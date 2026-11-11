using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The letters of a face measured for laying an MTEXT out as AutoCAD lays it: how far each moves the pen and the box of
    /// its ink, for a text one unit high at the font's own width. An SHX font by its strokes; a TrueType font by the outlines
    /// of its file, read as the library reads them (<see cref="DxfTrueTypeFont"/>), since AutoCAD sets a line by its ink and
    /// not by its advances; where no file is found, by the canvas's measure
    /// (<see cref="CadTrueTypeFonts.MeasureWidth"/>) or 0.6 of the height a letter, the ink taken as the whole advance.
    /// Arial's A is 0.9107 wide and inked from -0.0027 to 0.9134, up to 1.
    /// </summary>
    internal sealed class CadTextInk
    {
        /// <summary>How far a hard space moves the pen in an SHX font, which has none: AutoCAD draws it from Arial, 569 units of its 1466 (0.97033 at 2.5 high).</summary>
        private const double HardSpace = 569.0 / 1466.0;

        private static readonly ConditionalWeakTable<CadFontFace, CadTextInk[]> Kept =
            new ConditionalWeakTable<CadFontFace, CadTextInk[]>();
        private static readonly Dictionary<string, DxfTrueTypeFont> Files = new Dictionary<string, DxfTrueTypeFont>(
            StringComparer.OrdinalIgnoreCase
        );

        private readonly CadFontFace _face;
        private readonly bool _bold;
        private readonly bool _italic;
        private readonly DxfTrueTypeFont _file;
        private readonly Dictionary<int, Letter> _letters = new Dictionary<int, Letter>();
        private readonly Dictionary<double, Dictionary<int, double[]>> _leans =
            new Dictionary<double, Dictionary<int, double[]>>();
        private double _drawFactor = double.NaN;

        private CadTextInk(CadFontFace face, bool bold, bool italic)
        {
            _face = face;
            _bold = bold;
            _italic = italic;
            _file = face.Shx == null ? FileOf(CadTrueTypeFonts.PathOf(face.Name, bold, italic)) : null;
        }

        /// <summary>Whether the face is an SHX font.</summary>
        internal bool IsShx => _face.Shx != null;

        /// <summary>Whether the ink of the letters is read, from an SHX font or a TrueType file; false where only their widths are known.</summary>
        internal bool IsRead => _face.Shx != null || _file != null;

        /// <summary>
        /// The factor the canvas's letters are widened by to be as wide as the file's: the canvas sets a letter 1/0.72 of the
        /// height to the em whatever the font, the file by its own capital, so that Arial comes 0.6 percent narrow; 1 for an SHX
        /// font, with no canvas measure or no file.
        /// </summary>
        internal double DrawFactor
        {
            get
            {
                if (double.IsNaN(_drawFactor))
                {
                    double factor = 1.0;
                    Func<string, bool, bool, string, double> measure = CadTrueTypeFonts.MeasureWidth;
                    DxfFont.Glyph h = _file?.GlyphOf('H');

                    if (measure != null && h != null)
                    {
                        double drawn = measure(_face.Name, _bold, _italic, "H");
                        factor = drawn > 1E-9 ? h.Advance / _file.Above / drawn : 1.0;
                    }

                    _drawFactor = factor > 0.5 && factor < 2.0 ? factor : 1.0;
                }

                return _drawFactor;
            }
        }

        /// <summary>The measure of a face in its regular, bold or italic cut, made once for the face.</summary>
        internal static CadTextInk Of(CadFontFace face, bool bold, bool italic)
        {
            CadTextInk[] cuts = Kept.GetValue(face, f => new CadTextInk[4]);
            int cut = face.Shx != null ? 0 : (bold ? 1 : 0) + (italic ? 2 : 0);

            lock (cuts)
            {
                return cuts[cut] ?? (cuts[cut] = new CadTextInk(face, cut % 2 == 1, cut >= 2));
            }
        }

        /// <summary>The letter at a place of some letters, read on past it: two units of a surrogate pair, or the two bytes of a big font's letter, are one.</summary>
        internal Letter Next(string text, ref int i)
        {
            int c = text[i];

            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                c = char.ConvertToUtf32(text[i], text[i + 1]);
            }

            bool keeps = _face.Big == null || c < 0x80;

            lock (_letters)
            {
                if (keeps && _letters.TryGetValue(c, out Letter kept))
                {
                    i += c > 0xFFFF ? 2 : 1;
                    return kept;
                }

                Letter letter = _face.Shx != null ? OfStrokes(text, ref i, c) : OfOutline(text, ref i, c);

                if (keeps)
                {
                    _letters[c] = letter;
                }

                return letter;
            }
        }

        /// <summary>
        /// The left and right of a letter's ink when it leans: each point moved along the line by its height times the lean
        /// (the tangent of the oblique angle over the width factor). At 15 degrees ghsimplex's t reaches 0.18 of the height
        /// further right. Left as given where the letter's points are not known.
        /// </summary>
        internal void Lean(string text, int i, double lean, ref double left, ref double right)
        {
            int c = text[i];

            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                c = char.ConvertToUtf32(text[i], text[i + 1]);
            }

            bool keeps = _face.Big == null || c < 0x80;

            lock (_letters)
            {
                if (!_leans.TryGetValue(lean, out Dictionary<int, double[]> ofLean))
                {
                    ofLean = new Dictionary<int, double[]>();
                    _leans[lean] = ofLean;
                }

                if (!keeps || !ofLean.TryGetValue(c, out double[] span))
                {
                    span = Leaning(text, i, c, lean);

                    if (keeps)
                    {
                        ofLean[c] = span;
                    }
                }

                if (span != null)
                {
                    left = span[0];
                    right = span[1];
                }
            }
        }

        private double[] Leaning(string text, int i, int c, double lean)
        {
            bool inked = false;
            double left = 0.0;
            double right = 0.0;

            if (_face.Shx != null)
            {
                ShxFont from = null;
                ShxGlyph glyph = c == 0xA0 ? null : ShxText.Pick(_face, text, ref i, out from);

                if (glyph == null || from == null)
                {
                    return null;
                }

                double scale = 1.0 / from.Above;

                foreach (double[] stroke in glyph.Strokes)
                {
                    for (int p = 0; p + 1 < stroke.Length; p += 2)
                    {
                        double x = (stroke[p] + (stroke[p + 1] * lean)) * scale;
                        left = inked ? Math.Min(left, x) : x;
                        right = inked ? Math.Max(right, x) : x;
                        inked = true;
                    }
                }
            }
            else
            {
                DxfFont.Glyph glyph = _file?.GlyphOf(c);

                if (glyph == null || !glyph.HasInk)
                {
                    return null;
                }

                double scale = 1.0 / _file.Above;

                for (int k = 0; k < glyph.Xs.Length; k++)
                {
                    double x = (glyph.Xs[k] + (glyph.Ys[k] * lean)) * scale;
                    left = inked ? Math.Min(left, x) : x;
                    right = inked ? Math.Max(right, x) : x;
                    inked = true;
                }
            }

            return inked ? new[] { left, right } : null;
        }

        private Letter OfStrokes(string text, ref int i, int c)
        {
            if (c == 0xA0)
            {
                i++;
                return new Letter(HardSpace, 0.0, 0.0, 0.0, 0.0, false);
            }

            ShxGlyph glyph = ShxText.Pick(_face, text, ref i, out ShxFont from);

            if (glyph == null)
            {
                return new Letter(0.0, 0.0, 0.0, 0.0, 0.0, false);
            }

            double scale = 1.0 / from.Above;
            bool inked = false;
            double left = 0.0;
            double right = 0.0;
            double bottom = 0.0;
            double top = 0.0;

            foreach (double[] stroke in glyph.Strokes)
            {
                for (int p = 0; p + 1 < stroke.Length; p += 2)
                {
                    double x = stroke[p] * scale;
                    double y = stroke[p + 1] * scale;
                    left = inked ? Math.Min(left, x) : x;
                    right = inked ? Math.Max(right, x) : x;
                    bottom = inked ? Math.Min(bottom, y) : y;
                    top = inked ? Math.Max(top, y) : y;
                    inked = true;
                }
            }

            return new Letter(glyph.Advance * scale, left, right, bottom, top, inked);
        }

        private Letter OfOutline(string text, ref int i, int c)
        {
            int from = i;
            i += c > 0xFFFF ? 2 : 1;
            bool blank = c == ' ' || c == 0xA0 || c == '\t';
            DxfFont.Glyph glyph = _file?.GlyphOf(c == '\t' ? ' ' : c);

            if (glyph == null)
            {
                Func<string, bool, bool, string, double> measure = CadTrueTypeFonts.MeasureWidth;
                double advance =
                    measure != null ? measure(_face.Name, _bold, _italic, text.Substring(from, i - from)) : 0.6;
                return new Letter(advance, 0.0, blank ? 0.0 : advance, 0.0, blank ? 0.0 : 1.0, !blank);
            }

            double scale = 1.0 / _file.Above;

            if (blank || !glyph.HasInk)
            {
                return new Letter(glyph.Advance * scale, 0.0, 0.0, 0.0, 0.0, false);
            }

            double left = glyph.Xs[0];
            double right = left;
            double bottom = glyph.Ys[0];
            double top = bottom;

            for (int k = 1; k < glyph.Xs.Length; k++)
            {
                left = Math.Min(left, glyph.Xs[k]);
                right = Math.Max(right, glyph.Xs[k]);
                bottom = Math.Min(bottom, glyph.Ys[k]);
                top = Math.Max(top, glyph.Ys[k]);
            }

            return new Letter(glyph.Advance * scale, left * scale, right * scale, bottom * scale, top * scale, true);
        }

        /// <summary>The font of a file, read once; null where there is none, it cannot be read or its outlines are not TrueType's.</summary>
        private static DxfTrueTypeFont FileOf(string path)
        {
            if (path == null)
            {
                return null;
            }

            lock (Files)
            {
                if (!Files.TryGetValue(path, out DxfTrueTypeFont font))
                {
                    try
                    {
                        font = DxfTrueTypeFont.Read(File.ReadAllBytes(path));
                    }
                    catch (Exception exception)
                        when (exception is IOException
                            || exception is UnauthorizedAccessException
                            || exception is ArgumentException
                            || exception is IndexOutOfRangeException
                        )
                    {
                        GeometryHelperLog.Warn("CadTextInk: the font " + path + " could not be read.", exception);
                        font = null;
                    }

                    Files[path] = font;
                }

                return font;
            }
        }

        /// <summary>A letter one unit high: how far it moves the pen, and the box of its ink from where the pen stood on the baseline.</summary>
        internal struct Letter
        {
            internal Letter(double advance, double left, double right, double bottom, double top, bool hasInk)
            {
                Advance = advance;
                Left = left;
                Right = right;
                Bottom = bottom;
                Top = top;
                HasInk = hasInk;
            }

            internal double Advance { get; }

            internal double Left { get; }

            internal double Right { get; }

            internal double Bottom { get; }

            internal double Top { get; }

            internal bool HasInk { get; }
        }
    }
}
