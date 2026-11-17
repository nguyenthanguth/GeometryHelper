using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The outlines of a text drawn in a TrueType font, for the commands that turn words into lines (TXTEXP): the contours of
    /// each letter of the font's file (<see cref="DxfTrueTypeFont.OutlineOf"/>), set along the baseline by their advances at the
    /// text's height (the top of the font's A is one height up), the width factor across, each letter leaning by the slant from
    /// the baseline, the whole held at its alignment point as the display holds a text (left, centre or right by the advances
    /// of the words; baseline, bottom, middle or top by the height and the font's depth), turned over where drawn backwards, then
    /// turned and put in the drawing. An SHX font has its strokes already (<see cref="DisplayText2.Strokes"/>).
    /// </summary>
    internal static class CadTextOutlines
    {
        // The faces a letter the font lacks is looked for in, in this order, as the canvas does (CadGlyphFallback).
        private static readonly CadFontFace[] Fallbacks = new[] { "Arial", "Segoe UI", "Tahoma", "Times New Roman" }
            .Select(name => new CadFontFace(name, null, null, null))
            .ToArray();

        /// <summary>The first of the fallback fonts, in the weight and slant asked, that has a letter; null where none has it.</summary>
        private static DxfTrueTypeFont Fallback(int number, bool bold, bool italic)
        {
            foreach (CadFontFace face in Fallbacks)
            {
                DxfTrueTypeFont file = CadTextInk.Of(face, bold, italic).TrueType;

                if (file != null && file.Above > 0.0 && file.GlyphOf(number) != null)
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>The contours of a letter taken from a font of another size, as high as the font's own capitals; the contours themselves where the factor is 1.</summary>
        private static IReadOnlyList<double[]> Scaled(IReadOnlyList<double[]> outline, double factor)
        {
            if (factor == 1.0)
            {
                return outline;
            }

            return outline.Select(contour => contour.Select(v => v * factor).ToArray()).ToList();
        }

        /// <summary>
        /// The closed contours a text of a TrueType font is made of, each as x, y, x, y in the drawing; null where the font's file
        /// is not found or has no outlines to read (an OpenType file of CFF outlines), an empty list for words of no ink.
        /// </summary>
        internal static List<double[]> Contours(DisplayText2 text)
        {
            var face = new CadFontFace(text.FontName, null, null, null);
            DxfTrueTypeFont file = CadTextInk.Of(face, text.IsBold, text.IsItalic).TrueType;

            if (file == null || !(file.Above > 0.0) || !(text.Height > 0.0))
            {
                return null;
            }

            double scale = text.Height / file.Above;
            double across = scale * (text.WidthFactor > 0.0 ? text.WidthFactor : 1.0);
            double slant = Math.Tan(text.Oblique);
            var letters = new List<KeyValuePair<double, IReadOnlyList<double[]>>>();
            double pen = 0.0;
            string words = text.Text ?? string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                int number = words[i];

                if (char.IsHighSurrogate(words[i]) && i + 1 < words.Length && char.IsLowSurrogate(words[i + 1]))
                {
                    number = char.ConvertToUtf32(words[i], words[i + 1]);
                    i++;
                }

                if (number < 32)
                {
                    continue;
                }

                DxfTrueTypeFont from = file;
                DxfFont.Glyph glyph = file.GlyphOf(number);

                // A letter the font lacks is taken from the font that has it, its capitals as high as the font's own.
                if (glyph == null && number >= 0x80 && !(number <= 0xFFFF && char.IsWhiteSpace((char)number)))
                {
                    from = Fallback(number, text.IsBold, text.IsItalic) ?? file;
                    glyph = from.GlyphOf(number);
                }

                double unit = ReferenceEquals(from, file) ? 1.0 : file.Above / from.Above;
                IReadOnlyList<double[]> outline = from.OutlineOf(number);

                if (outline != null && outline.Count > 0)
                {
                    letters.Add(new KeyValuePair<double, IReadOnlyList<double[]>>(pen, Scaled(outline, unit)));
                }

                pen += glyph != null ? glyph.Advance * unit : file.Above / 2.0;
            }

            double start =
                text.Horizontal == 1 ? -pen * across / 2.0
                : text.Horizontal == 2 ? -pen * across
                : 0.0;
            double below = double.IsNaN(file.Below) ? 0.0 : file.Below;
            double up =
                text.Vertical == 1 ? text.Height * below / file.Above
                : text.Vertical == 2 ? -text.Height / 2.0
                : text.Vertical == 3 ? -text.Height
                : 0.0;
            double cos = Math.Cos(text.Rotation);
            double sin = Math.Sin(text.Rotation);
            var contours = new List<double[]>();

            foreach (KeyValuePair<double, IReadOnlyList<double[]>> letter in letters)
            {
                foreach (double[] contour in letter.Value)
                {
                    var made = new double[contour.Length];

                    for (int p = 0; p < contour.Length; p += 2)
                    {
                        double high = contour[p + 1] * scale;
                        double along = start + ((letter.Key + contour[p]) * across) + (high * slant);

                        if (text.IsBackward)
                        {
                            along = -along;
                        }

                        double rise = high + up;
                        made[p] = text.X + (along * cos) - (rise * sin);
                        made[p + 1] = text.Y + (along * sin) + (rise * cos);
                    }

                    contours.Add(made);
                }
            }

            return contours;
        }
    }
}
