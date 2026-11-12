using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Lays the letters of a text out in an SHX font as AutoCAD draws them: each letter's strokes scaled so that the font's
    /// height above its baseline is the text's height, the next letter where the pen ended, the width factor taken across, the
    /// slant leaning each letter from its baseline, the lines 1.25 heights apart as the canvas sets TrueType text, then the
    /// whole put at its alignment point, turned over along its baseline where it is drawn backwards, and turned to its angle.
    /// </summary>
    /// <remarks>
    /// A line is justified by the box of its ink, as AutoCAD justifies a TEXT, upright and at its width factor: right by the
    /// ink's right, centred on the middle of the ink; a line ending in a space by the box from the start of its baseline to
    /// its last pen. romans.shx "ABC123" 10 high, right on x 100, is inked from 44.762 to 100, as the library's measuring,
    /// checked against AutoCAD 2021, puts it.
    /// </remarks>
    internal static class ShxText
    {
        /// <summary>How far left of its letters' pens AutoCAD 2021 starts and ends a line under, over or through the letters of a TEXT in an SHX font, over the height, across at the width factor.</summary>
        private const double MarkShift = 0.15;

        /// <summary>
        /// The strokes of a text in the drawing's coordinates, and the width of its widest line: from the start of the
        /// baseline to the last pen for a line on the left, the width of its box for one centred or on the right. A letter
        /// neither font has is drawn as the font's question mark; a space the font has not moves the pen by its A.
        /// </summary>
        /// <remarks>
        /// <paramref name="marks"/>, where given, says of each letter of the text whether it is lined under (1), through (2)
        /// or over (4), as a TEXT's <c>%%U</c>, <c>%%K</c> and <c>%%O</c> line it; null for none. AutoCAD 2021 draws such a
        /// line 0.2 of the height below the baseline, half the height above it or 1.2 heights above it, along the pens of the
        /// letters lined but 0.15 of the height (at the width factor) left of them, slanted as the letters are, and boxes the
        /// words with the line for their justification (probed: simplex 10 high, "%%uA" is lined from -1.5 to 8.976, the
        /// pen 10.476 on; "%%uAB" centred on nought starts at -8.738, not at the -8.571 of "AB").
        /// </remarks>
        internal static List<double[]> Strokes(
            CadFontFace face,
            string text,
            double x,
            double y,
            double height,
            double rotation,
            double widthFactor,
            int horizontal,
            int vertical,
            double oblique,
            bool backward,
            double descent,
            out double width,
            byte[] marks = null
        )
        {
            string[] lines = text.Split('\n');
            double step = 1.25 * height;
            double top = height;
            double bottom = -(lines.Length - 1) * step;
            double shift =
                vertical == 0 ? 0.0
                : vertical == 1 ? -(bottom - descent)
                : vertical == 2 ? -(top + bottom) / 2.0
                : -top;
            double slant = Math.Tan(oblique);
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            var strokes = new List<double[]>();
            var placed = new List<KeyValuePair<ShxGlyph, double>>();
            var lefts = new List<double>();
            var lined = new List<byte>();
            double markShift = MarkShift * height * widthFactor;
            int offset = 0;
            width = 0.0;

            for (int l = 0; l < lines.Length; l++)
            {
                string line = lines[l];
                placed.Clear();
                lefts.Clear();
                lined.Clear();
                double pen = 0.0;
                int i = 0;

                while (i < line.Length)
                {
                    int at = offset + i;
                    ShxGlyph glyph = Pick(face, line, ref i, out ShxFont from);

                    if (glyph == null)
                    {
                        continue;
                    }

                    double scale = height / from.Above;
                    placed.Add(new KeyValuePair<ShxGlyph, double>(glyph, scale));
                    lefts.Add(pen);
                    lined.Add(marks != null && at < marks.Length ? marks[at] : (byte)0);
                    pen += glyph.Advance * scale * widthFactor;
                }

                offset += line.Length + 1;

                // The box the line is justified by: its ink upright, or from its start to its last pen where it ends in a space.
                double inkLeft = double.MaxValue;
                double inkRight = double.MinValue;

                for (int k = 0; k < placed.Count; k++)
                {
                    foreach (double[] stroke in placed[k].Key.Strokes)
                    {
                        for (int p = 0; p + 1 < stroke.Length; p += 2)
                        {
                            double along = lefts[k] + (stroke[p] * placed[k].Value * widthFactor);
                            inkLeft = Math.Min(inkLeft, along);
                            inkRight = Math.Max(inkRight, along);
                        }
                    }
                }

                bool inked = inkLeft <= inkRight;
                bool spaced = line.Length > 0 && line[line.Length - 1] == ' ';
                double boxLeft =
                    spaced ? (inked ? Math.Min(0.0, inkLeft) : 0.0)
                    : inked ? inkLeft
                    : 0.0;
                double boxRight =
                    spaced ? (inked ? Math.Max(pen, inkRight) : pen)
                    : inked ? inkRight
                    : 0.0;
                bool anyLined = false;

                // The lines under, over and through letters box the words with their ink.
                for (int k = 0; k < placed.Count; k++)
                {
                    if (lined[k] != 0)
                    {
                        double from = lefts[k] - markShift;
                        double to = lefts[k] + (placed[k].Key.Advance * placed[k].Value * widthFactor) - markShift;
                        boxLeft = inked || anyLined ? Math.Min(boxLeft, from) : from;
                        boxRight = inked || anyLined ? Math.Max(boxRight, to) : to;
                        anyLined = true;
                    }
                }
                width = Math.Max(
                    width,
                    horizontal == 0 ? pen
                        : horizontal == 1 ? boxRight - boxLeft
                        : boxRight
                );
                double left =
                    horizontal == 0 ? 0.0
                    : horizontal == 1 ? -(boxLeft + boxRight) / 2.0
                    : -boxRight;
                double baseline = shift - (l * step);

                for (int k = 0; k < placed.Count; k++)
                {
                    double scale = placed[k].Value;

                    foreach (double[] stroke in placed[k].Key.Strokes)
                    {
                        var drawn = new double[stroke.Length];

                        for (int p = 0; p + 1 < stroke.Length; p += 2)
                        {
                            double up = stroke[p + 1] * scale;
                            double along = left + lefts[k] + (stroke[p] * scale * widthFactor) + (up * slant);
                            double ly = baseline + up;
                            double lx = backward ? -along : along;
                            drawn[p] = x + (lx * cos) - (ly * sin);
                            drawn[p + 1] = y + (lx * sin) + (ly * cos);
                        }

                        strokes.Add(drawn);
                    }
                }

                if (!anyLined)
                {
                    continue;
                }

                // Each line along the letters it lines, one stroke for the letters next to each other lined alike.
                foreach (byte mark in new byte[] { 1, 2, 4 })
                {
                    double up =
                        (
                            mark == 1 ? -0.2
                            : mark == 2 ? 0.5
                            : 1.2
                        ) * height;

                    for (int k = 0; k < placed.Count; k++)
                    {
                        if ((lined[k] & mark) == 0)
                        {
                            continue;
                        }

                        int last = k;

                        while (last + 1 < placed.Count && (lined[last + 1] & mark) != 0)
                        {
                            last++;
                        }

                        double from = left + lefts[k] - markShift + (up * slant);
                        double to =
                            left
                            + lefts[last]
                            + (placed[last].Key.Advance * placed[last].Value * widthFactor)
                            - markShift
                            + (up * slant);
                        double ly = baseline + up;
                        double fx = backward ? -from : from;
                        double tx = backward ? -to : to;
                        strokes.Add(
                            new[]
                            {
                                x + (fx * cos) - (ly * sin),
                                y + (fx * sin) + (ly * cos),
                                x + (tx * cos) - (ly * sin),
                                y + (tx * sin) + (ly * cos),
                            }
                        );
                        k = last;
                    }
                }
            }

            return strokes;
        }

        /// <summary>
        /// The letter at a place of a line, read on past it, and the font it is drawn from: the font's own letter; beside a big
        /// font the big font's letter of the text's bytes in its code page, a byte that opens a pair taken with the next; else
        /// the question mark of the font, or of the big font. A space the font has not moves the pen by its A or its height.
        /// </summary>
        internal static ShxGlyph Pick(CadFontFace face, string line, ref int i, out ShxFont from)
        {
            int c = line[i];

            if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
            {
                c = char.ConvertToUtf32(line[i], line[i + 1]);
                i++;
            }

            i++;
            c = c == '\t' ? ' ' : c;
            ShxFont font = face.Shx;
            ShxGlyph glyph = Own(font, c);
            from = font;

            if (glyph == null && face.Big != null)
            {
                glyph = Big(face, c, line, ref i);
                from = face.Big;
            }

            if (glyph == null && c == ' ')
            {
                from = font;
                return new ShxGlyph(font.GlyphOf('A')?.Advance ?? font.Above, Array.Empty<double[]>());
            }

            if (glyph == null)
            {
                glyph = font.GlyphOf('?');
                from = font;

                if (glyph == null && face.Big != null)
                {
                    glyph = face.Big.GlyphOf('?');
                    from = face.Big;
                }
            }

            return glyph;
        }

        /// <summary>A letter of the font: by its byte in a font of shapes, °, ± and Ø as 127, 128 and 129; by its Unicode number in a unifont, Ø as ∅ first.</summary>
        private static ShxGlyph Own(ShxFont font, int c)
        {
            int sign =
                c == 0xB0 ? 127
                : c == 0xB1 ? 128
                : c == 0xD8 || c == 0x2205 ? 129
                : 0;

            if (font.Kind == ShxFontKind.Shapes)
            {
                return sign != 0 ? font.GlyphOf(sign) ?? font.GlyphOf(c)
                    : c < 256 ? font.GlyphOf(c)
                    : null;
            }

            return (c == 0xD8 ? font.GlyphOf(0x2205) : null)
                ?? font.GlyphOf(c)
                ?? (sign != 0 ? font.GlyphOf(sign) : null);
        }

        /// <summary>A letter of the big font: the text's letter as bytes of the big font's code page, ASCII as itself; one of two bytes, or a byte that opens a pair with the next letter's byte, or a byte alone.</summary>
        private static ShxGlyph Big(CadFontFace face, int c, string line, ref int i)
        {
            byte[] bytes = BytesOf(face, c);

            if (bytes == null)
            {
                return null;
            }

            if (bytes.Length == 2)
            {
                return face.Big.GlyphOf((bytes[0] << 8) | bytes[1]);
            }

            if (bytes.Length == 1 && face.Big.Opens(bytes[0]) && i < line.Length)
            {
                byte[] next = BytesOf(face, line[i]);

                if (next != null && next.Length == 1)
                {
                    i++;
                    return face.Big.GlyphOf((bytes[0] << 8) | next[0]);
                }
            }

            return bytes.Length == 1 ? face.Big.GlyphOf(bytes[0]) : null;
        }

        private static byte[] BytesOf(CadFontFace face, int c)
        {
            if (c < 0x80)
            {
                return new[] { (byte)c };
            }

            if (face.Page == null || c > 0x10FFFF || (c >= 0xD800 && c <= 0xDFFF))
            {
                return null;
            }

            return face.Page.GetBytes(char.ConvertFromUtf32(c));
        }
    }
}
