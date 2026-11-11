using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// AutoCAD's wild-card patterns (<c>wcmatch</c>), which -PURGE, -GROUP ?, Quick Select's * Wildcard Match and FIND with
    /// Use wildcards take: <c>*</c> any run of characters, <c>?</c> one character, <c>#</c> a digit, <c>@</c> a letter,
    /// <c>.</c> a character neither a letter nor a digit, <c>[AB]</c> one of those, <c>[~AB]</c> one not of those, a
    /// leading <c>~</c> anything the rest does not match, a comma between patterns any of them, and a back quote before a
    /// character that character itself. <c>WALL*,DOOR?</c> matches WALL-EXT and DOOR1.
    /// </summary>
    internal static class CadWildcard
    {
        /// <summary>Gets whether a text matches a pattern as a whole.</summary>
        internal static bool IsMatch(string text, string pattern, bool ignoreCase) =>
            ToRegex(pattern, ignoreCase, true).IsMatch(text ?? string.Empty);

        /// <summary>
        /// The regular expression of a pattern: anchored at both ends for a whole text, else matching a run inside a text (a
        /// leading ~ is then taken as a letter).
        /// </summary>
        internal static Regex ToRegex(string pattern, bool ignoreCase, bool whole)
        {
            var alternatives = new List<string>();
            var negated = new List<bool>();
            var part = new StringBuilder();
            string text = pattern ?? string.Empty;
            bool atStart = true;
            bool negate = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '`' && i + 1 < text.Length)
                {
                    part.Append(Regex.Escape(text[++i].ToString()));
                    atStart = false;
                    continue;
                }

                if (ch == '~' && atStart && whole)
                {
                    negate = true;
                    atStart = false;
                    continue;
                }

                atStart = false;

                switch (ch)
                {
                    case ',':
                        alternatives.Add(part.ToString());
                        negated.Add(negate);
                        part.Clear();
                        negate = false;
                        atStart = true;
                        break;
                    case '*':
                        part.Append(".*");
                        break;
                    case '?':
                        part.Append('.');
                        break;
                    case '#':
                        part.Append("[0-9]");
                        break;
                    case '@':
                        part.Append("\\p{L}");
                        break;
                    case '.':
                        part.Append("[^\\p{L}0-9]");
                        break;
                    case '[':
                        int close = text.IndexOf(']', i + 2);

                        if (close < 0)
                        {
                            part.Append("\\[");
                            break;
                        }

                        string inside = text.Substring(i + 1, close - i - 1);
                        bool not = inside.StartsWith("~", StringComparison.Ordinal);
                        part.Append(not ? "[^" : "[");

                        foreach (char member in not ? inside.Substring(1) : inside)
                        {
                            part.Append(member == '-' ? "-" : Regex.Escape(member.ToString()).Replace("]", "\\]"));
                        }

                        part.Append(']');
                        i = close;
                        break;
                    default:
                        part.Append(Regex.Escape(ch.ToString()));
                        break;
                }
            }

            alternatives.Add(part.ToString());
            negated.Add(negate);
            var all = new StringBuilder();

            for (int i = 0; i < alternatives.Count; i++)
            {
                if (i > 0)
                {
                    all.Append('|');
                }

                string body = whole ? "^(?:" + alternatives[i] + ")$" : "(?:" + alternatives[i] + ")";
                all.Append(negated[i] ? "^(?!" + body + ").*$" : body);
            }

            RegexOptions options =
                RegexOptions.CultureInvariant
                | RegexOptions.Singleline
                | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return new Regex(all.ToString(), options);
        }
    }
}
