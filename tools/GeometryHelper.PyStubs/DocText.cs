using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// Turns the XML of a documentation comment into the plain text of a docstring: the tags become their words
    /// (<c>&lt;see cref="T:A.B.C"/&gt;</c> is <c>C</c>), <c>&lt;para&gt;</c> becomes a blank line, runs of white space become one
    /// space, and the lines are wrapped to fit inside a stub.
    /// </summary>
    internal static class DocText
    {
        /// <summary>The widest a line of a stub may be.</summary>
        public const int LineWidth = 116;

        private const char ParagraphBreak = '\u0001';
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex Arity = new Regex(@"`+\d+", RegexOptions.Compiled);

        /// <summary>The paragraphs of the text inside an element, each with its white space collapsed. Empty ones are dropped.</summary>
        public static List<string> Paragraphs(XElement element)
        {
            var text = new StringBuilder();
            AppendChildren(element, text);
            return text.ToString()
                .Split(ParagraphBreak)
                .Select(p => Spaces.Replace(p, " ").Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }

        /// <summary>
        /// The docstring lines, with the quotes, for <paramref name="paragraphs"/> written at an indent of
        /// <paramref name="indent"/> spaces: one line when it fits, else the text wrapped so that no line passes
        /// <see cref="LineWidth"/> columns, with an empty line between paragraphs. <c>\</c> and <c>"</c> are escaped.
        /// </summary>
        public static List<string> DocstringLines(IReadOnlyList<string> paragraphs, int indent)
        {
            string pad = new string(' ', indent);
            string[] escaped = paragraphs.Select(Escape).ToArray();
            int width = LineWidth - indent - 3;
            List<string> lines = Wrap(escaped, width);
            if (lines.Count == 1 && lines[0].Length > width - 3)
            {
                lines = Wrap(escaped, width - 3);
            }

            var result = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string prefix = i == 0 ? "\"\"\"" : "";
                string suffix = i == lines.Count - 1 ? "\"\"\"" : "";
                string full = prefix + line + suffix;
                result.Add(full.Length == 0 ? "" : pad + full);
            }

            return result;
        }

        private static List<string> Wrap(string[] paragraphs, int width)
        {
            var lines = new List<string>();
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                {
                    lines.Add("");
                }

                var line = new StringBuilder();
                foreach (string word in paragraphs[p].Split(' '))
                {
                    if (line.Length > 0 && line.Length + 1 + word.Length > width)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }

                    if (line.Length > 0)
                    {
                        line.Append(' ');
                    }

                    line.Append(word);
                }

                lines.Add(line.ToString());
            }

            return lines;
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void AppendChildren(XElement element, StringBuilder text)
        {
            foreach (XNode node in element.Nodes())
            {
                if (node is XText plain)
                {
                    text.Append(plain.Value);
                }
                else if (node is XElement child)
                {
                    AppendElement(child, text);
                }
            }
        }

        private static void AppendElement(XElement element, StringBuilder text)
        {
            switch (element.Name.LocalName)
            {
                case "see":
                case "seealso":
                    string label = element.Value;
                    if (label.Trim().Length > 0)
                    {
                        text.Append(label);
                    }
                    else if (element.Attribute("cref") != null)
                    {
                        text.Append(CrefName(element.Attribute("cref").Value));
                    }
                    else
                    {
                        text.Append(element.Attribute("langword")?.Value ?? element.Attribute("href")?.Value);
                    }

                    break;
                case "paramref":
                case "typeparamref":
                    text.Append(element.Attribute("name")?.Value);
                    break;
                case "para":
                case "item":
                    text.Append(ParagraphBreak);
                    AppendChildren(element, text);
                    text.Append(ParagraphBreak);
                    break;
                case "term":
                case "description":
                    AppendChildren(element, text);
                    text.Append(' ');
                    break;
                default:
                    AppendChildren(element, text);
                    break;
            }
        }

        /// <summary>
        /// The name to show for a <c>cref</c>: the last part of a type (<c>T:A.B.C</c> is <c>C</c>) and the last two of a
        /// member (<c>M:A.B.C.M(System.Int32)</c> is <c>C.M</c>, a constructor is just <c>C</c>). Generic arguments and the
        /// parameter list are dropped.
        /// </summary>
        public static string CrefName(string cref)
        {
            string target = cref.StartsWith("!:", StringComparison.Ordinal) ? cref.Substring(2) : cref;
            bool isType = target.Length < 2 || target[1] != ':' || target[0] == 'T' || target[0] == 'N';
            if (target.Length >= 2 && target[1] == ':')
            {
                target = target.Substring(2);
            }

            int parameters = target.IndexOf('(');
            if (parameters >= 0)
            {
                target = target.Substring(0, parameters);
            }

            target = Arity.Replace(StripBraces(target), "");
            string[] parts = target.Split('.');
            if (isType || parts.Length < 2)
            {
                return parts[parts.Length - 1];
            }

            string member = parts[parts.Length - 1];
            return member == "#ctor" ? parts[parts.Length - 2] : parts[parts.Length - 2] + "." + member;
        }

        private static string StripBraces(string text)
        {
            var result = new StringBuilder();
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                }
                else if (depth == 0)
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}
