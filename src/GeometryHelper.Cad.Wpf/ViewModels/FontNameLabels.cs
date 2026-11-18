using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// How the font boxes name a TrueType family: its English name first, then the other names its name table holds (Korean,
    /// Japanese, Chinese ...), as <c>DotumChe  돋움체</c>; a family with one name stays as it is. Only the label and the search
    /// are made here; the name a style stores is the family the user picked, as before.
    /// </summary>
    internal static class FontNameLabels
    {
        /// <summary>The space between one name and the next in a label.</summary>
        internal const string Gap = "  ";

        /// <summary>
        /// The names to show after the English one: each once, the English one and a name of letters of either case the same
        /// as an earlier one left out, empty ones too, in the order given.
        /// </summary>
        internal static IReadOnlyList<string> Others(string english, IEnumerable<string> names)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { (english ?? string.Empty).Trim() };
            var others = new List<string>();

            foreach (string name in names ?? Array.Empty<string>())
            {
                string trimmed = (name ?? string.Empty).Trim();

                if (trimmed.Length > 0 && seen.Add(trimmed))
                {
                    others.Add(trimmed);
                }
            }

            return others;
        }

        /// <summary>The other names of an installed family, from its name table; none for a name that is no family found.</summary>
        internal static IReadOnlyList<string> OthersOf(string family) =>
            Others(family, CadTrueTypeFonts.LocalNamesOf(family));

        /// <summary>The label: the English name, then the others with <see cref="Gap"/> between; the English name alone where there are none.</summary>
        internal static string Label(string english, IEnumerable<string> others) =>
            string.Join(Gap, new[] { english }.Concat(Others(english, others)));

        /// <summary>The other names alone, as the box shows them after the English one: empty where there are none.</summary>
        internal static string LocalText(string family) => string.Join(Gap, OthersOf(family));

        /// <summary>Whether what was typed starts the English name or one of the others, letters of either case the same.</summary>
        internal static bool Matches(string english, IEnumerable<string> others, string typed)
        {
            if (string.IsNullOrEmpty(typed))
            {
                return false;
            }

            return new[] { english }
                .Concat(others ?? Array.Empty<string>())
                .Any(n => n != null && n.StartsWith(typed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
