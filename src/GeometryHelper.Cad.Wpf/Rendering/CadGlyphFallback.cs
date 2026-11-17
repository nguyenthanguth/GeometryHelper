using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Splits the letters of a text drawn in a TrueType face into runs, each in a face that has its glyph, as AutoCAD 2021 draws
    /// a letter its font lacks through the font linking of Windows. The canvas draws a letter a face has no glyph for as the
    /// face's empty box: the Vietnamese letters of U+1EA0 to U+1EF9 (ạ ỗ ợ ...) in Gulim or Malgun Gothic, which an MTEXT made
    /// on a Korean machine names for them (<c>\fGulim|b0|i0|c129|p50;ỗ</c>).
    /// </summary>
    /// <remarks>
    /// A letter the face has stays in the face. One it lacks is looked for in Arial, Segoe UI, Tahoma and Times New Roman, in
    /// that order, in the face's weight and slant, then in the font Windows gives for it; where none has it, it stays in the
    /// face, which draws its box. The letters of ASCII and the spaces are never moved.
    /// </remarks>
    internal static class CadGlyphFallback
    {
        private static readonly string[] Families = { "Arial", "Segoe UI", "Tahoma", "Times New Roman" };

        // The face found for a letter by its weight, slant and number; null for a letter no face has. Kept for good, as the
        // typefaces the canvas matches are not disposed.
        private static readonly Dictionary<string, SKTypeface> Chosen = new Dictionary<string, SKTypeface>();

        // The face of each family in a weight and slant, null for a family the machine has not, so that letters drawn in one
        // family are one run.
        private static readonly Dictionary<string, SKTypeface> Matched = new Dictionary<string, SKTypeface>();

        /// <summary>
        /// The runs of a line, each the letters one face draws, in their order: the face's own, or the face the letters it lacks
        /// are drawn in. A line of letters the face has, or of ASCII, is one run in the face itself.
        /// </summary>
        /// <param name="face">The face of the text.</param>
        /// <param name="text">The line.</param>
        /// <returns>The runs, each its letters and the face they are drawn in; one run for an empty line.</returns>
        internal static List<KeyValuePair<string, SKTypeface>> Runs(SKTypeface face, string text)
        {
            var runs = new List<KeyValuePair<string, SKTypeface>>();
            text = text ?? string.Empty;
            int from = 0;

            while (from < text.Length && text[from] < 0x80)
            {
                from++;
            }

            if (from == text.Length)
            {
                runs.Add(new KeyValuePair<string, SKTypeface>(text, face));
                return runs;
            }

            using (var own = new SKFont(face))
            {
                var run = new StringBuilder();
                SKTypeface runFace = face;

                for (int i = 0; i < text.Length; i++)
                {
                    int c = text[i];
                    int length = 1;

                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        c = char.ConvertToUtf32(text[i], text[i + 1]);
                        length = 2;
                    }

                    SKTypeface drawn =
                        c < 0x80 || char.IsWhiteSpace(text, i) || own.ContainsGlyph(c) ? face : Other(face, c) ?? face;

                    if (run.Length > 0 && !ReferenceEquals(drawn, runFace))
                    {
                        runs.Add(new KeyValuePair<string, SKTypeface>(run.ToString(), runFace));
                        run.Clear();
                    }

                    runFace = drawn;
                    run.Append(text, i, length);
                    i += length - 1;
                }

                runs.Add(new KeyValuePair<string, SKTypeface>(run.ToString(), runFace));
            }

            return runs;
        }

        /// <summary>The face that draws a letter a face lacks, in its weight and slant; null where none has it.</summary>
        private static SKTypeface Other(SKTypeface face, int c)
        {
            SKFontStyle style = face.FontStyle;
            string key = style.Weight + "|" + (int)style.Slant + "|" + c;

            lock (Chosen)
            {
                if (Chosen.TryGetValue(key, out SKTypeface found))
                {
                    return found;
                }

                foreach (string family in Families)
                {
                    SKTypeface candidate = Matching(family, style);

                    if (candidate != null && Has(candidate, c))
                    {
                        found = candidate;
                        break;
                    }
                }

                if (found == null)
                {
                    SKTypeface linked = SKFontManager.Default.MatchCharacter(face.FamilyName, style, null, c);
                    found = linked != null && Has(linked, c) ? Matching(linked.FamilyName, style) : null;
                }

                Chosen[key] = found;
                return found;
            }
        }

        private static SKTypeface Matching(string family, SKFontStyle style)
        {
            string key = style.Weight + "|" + (int)style.Slant + "|" + family;

            if (!Matched.TryGetValue(key, out SKTypeface face))
            {
                face = SKFontManager.Default.MatchFamily(family, style);
                Matched[key] = face;
            }

            return face;
        }

        private static bool Has(SKTypeface face, int c)
        {
            using (var font = new SKFont(face))
            {
                return font.ContainsGlyph(c);
            }
        }
    }
}
