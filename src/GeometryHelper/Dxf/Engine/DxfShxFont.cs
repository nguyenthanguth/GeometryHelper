using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A compiled SHX font of letters, of AutoCAD's forms <c>AutoCAD-86 shapes 1.0</c> or <c>1.1</c> (its shape 0 the font's
    /// name, how far its letters reach above and below the baseline, and its modes; each other shape a letter by its byte) and
    /// <c>AutoCAD-86 unifont 1.0</c> (the font's header first, then each letter by its Unicode number), read as AutoCAD 2021
    /// reads them (check F1a): each letter's advance, the pen's last x, and the points of its ink, from which its box is
    /// worked out at any width and slant. txt.shx reaches 6 above and 2 below, its A is inked from 0 to 4 and moves the pen 6.
    /// </summary>
    /// <remarks>
    /// The codes of a shape are those ezdxf's shapefile.py and AutoCAD's documentation give: a byte of 0x10 and over a move of
    /// its high half's length along one of 16 directions; 0 the end, 1 the pen down, 2 up, 3 and 4 the scale divided or
    /// multiplied by the next byte, 5 and 6 the pen's place pushed and popped, 7 a shape drawn within, 8 and 9 moves by
    /// signed bytes, 10 an arc of octants, 11 an arc of fractions of them, 12 and 13 arcs by their bulge and 14 the next code
    /// for vertical text alone. Read as AutoCAD draws them, where ezdxf reads them otherwise: the number of a shape within a
    /// unifont is big-endian; the pen put down marks the point it stands on, a move or not, as fonts end a letter by
    /// <c>8 dx dy 1 2</c> to mark its right side; the angles of an arc of fractions are whole degrees; and the octants of an
    /// arc are of a sign and a size, 0xA4 clockwise from octant 2 for 4. An arc is inked as AutoCAD boxes it, not as it runs:
    /// one under half a turn as its chord and the chord moved by its height, half a turn as that half circle's box, and one
    /// over half a turn as its whole circle's box. A file of another form, or one cut short, is no font.
    /// </remarks>
    internal sealed class DxfShxFont : DxfFont
    {
        // The directions of a move of a byte of 0x10 and over, by its low half, as AutoCAD's 16 directions run.
        private static readonly double[] StepX = { 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5, 0, 0.5, 1, 1 };
        private static readonly double[] StepY = { 0, 0.5, 1, 1, 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5 };

        // How deep shapes are drawn within shapes, the letter itself the first, as the throwaway reader of F1a stops; and how
        // many codes a letter runs at most, its shapes within it counted, so that a file drawing shapes within each other
        // over and over ends.
        private const int MostDepth = 16;
        private const int MostCodes = 100000;

        private readonly Dictionary<int, byte[]> _codes;
        private readonly Dictionary<int, Glyph> _glyphs = new Dictionary<int, Glyph>();

        private DxfShxFont(bool unifont, int above, int below, Dictionary<int, byte[]> codes)
        {
            IsUnifont = unifont;
            Above = above;
            Below = below;
            _codes = codes;
        }

        /// <summary>Whether the font is a unifont, its letters by their Unicode numbers; else of shapes 1.0 or 1.1, by their bytes.</summary>
        internal bool IsUnifont { get; }

        /// <summary>How far the font's capitals reach above the baseline, in its units: 6 for txt, 21 for romans.</summary>
        internal override double Above { get; }

        /// <summary>How far the font's letters reach below the baseline, in its units: 2 for txt, 7 for romans.</summary>
        internal override double Below { get; }

        /// <summary>
        /// Reads a font from the bytes of its file; null where they are no font of letters of shapes 1.0 or 1.1 or of a
        /// unifont, one cut short or of no height above its baseline, and where they are a big font, which is read elsewhere.
        /// </summary>
        internal static DxfShxFont Read(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            int end = Array.IndexOf(bytes, (byte)0x1A, 0, Math.Min(bytes.Length, 64));

            if (end < 0)
            {
                return null;
            }

            string header = Encoding.ASCII.GetString(bytes, 0, end);

            if (header.StartsWith("AutoCAD-86 shapes 1.0", StringComparison.Ordinal) || header.StartsWith("AutoCAD-86 shapes 1.1", StringComparison.Ordinal))
            {
                return Shapes(bytes, end + 1);
            }

            return header.StartsWith("AutoCAD-86 unifont 1.0", StringComparison.Ordinal) ? Unifont(bytes, end + 1) : null;
        }

        /// <summary>A font of shapes 1.0 or 1.1: the first and last numbers and the count, the number and length of each shape, then the shapes, each its name, a byte 0 and its codes.</summary>
        private static DxfShxFont Shapes(byte[] bytes, int at)
        {
            if (at + 6 > bytes.Length)
            {
                return null;
            }

            int count = U16(bytes, at + 4);
            at += 6;

            if (at + (count * 4) > bytes.Length)
            {
                return null;
            }

            var codes = new Dictionary<int, byte[]>();
            int data = at + (count * 4);
            int above = -1;
            int below = 0;

            for (int i = 0; i < count; i++)
            {
                int number = U16(bytes, at + (i * 4));
                int length = U16(bytes, at + (i * 4) + 2);

                if (data + length > bytes.Length)
                {
                    return null;
                }

                int zero = Array.IndexOf(bytes, (byte)0, data, length);

                if (zero >= 0 && number == 0 && zero + 2 < data + length)
                {
                    above = bytes[zero + 1];
                    below = bytes[zero + 2];
                }
                else if (zero >= 0 && number != 0 && !codes.ContainsKey(number))
                {
                    codes.Add(number, Slice(bytes, zero + 1, data + length));
                }

                data += length;
            }

            return above > 0 ? new DxfShxFont(false, above, below, codes) : null;
        }

        /// <summary>A unifont: the count and the length of its header, its header (name, a byte 0, above, below, modes, encoding, embedding), then each letter, its number, length, name, a byte 0 and its codes.</summary>
        private static DxfShxFont Unifont(byte[] bytes, int at)
        {
            if (at + 6 > bytes.Length)
            {
                return null;
            }

            int size = U16(bytes, at + 4);
            at += 6;

            if (at + size > bytes.Length)
            {
                return null;
            }

            int zero = Array.IndexOf(bytes, (byte)0, at, size);

            if (zero < 0 || zero + 2 >= at + size)
            {
                return null;
            }

            int above = bytes[zero + 1];
            int below = bytes[zero + 2];
            at += size;
            var codes = new Dictionary<int, byte[]>();

            while (at + 4 <= bytes.Length)
            {
                int number = U16(bytes, at);
                int length = U16(bytes, at + 2);
                at += 4;

                // A last letter cut short is passed over, those before it kept.
                if (at + length > bytes.Length)
                {
                    break;
                }

                int name = Array.IndexOf(bytes, (byte)0, at, length);

                if (name >= 0 && !codes.ContainsKey(number))
                {
                    codes.Add(number, Slice(bytes, name + 1, at + length));
                }

                at += length;
            }

            return above > 0 ? new DxfShxFont(true, above, below, codes) : null;
        }

        private static int U16(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

        private static byte[] Slice(byte[] bytes, int from, int to)
        {
            var slice = new byte[to - from];
            Array.Copy(bytes, from, slice, 0, slice.Length);
            return slice;
        }

        /// <summary>Whether the font has a letter of a number: a Unicode number in a unifont, a byte in a font of shapes.</summary>
        internal bool Has(int number) => number != 0 && _codes.ContainsKey(number);

        /// <summary>The letter of a number, drawn once; null where the font has none.</summary>
        internal override Glyph GlyphOf(int number)
        {
            if (!Has(number))
            {
                return null;
            }

            lock (_glyphs)
            {
                if (!_glyphs.TryGetValue(number, out Glyph glyph))
                {
                    var pen = new Pen();
                    Draw(number, pen);
                    glyph = new Glyph(pen.X, pen.Xs.ToArray(), pen.Ys.ToArray());
                    _glyphs.Add(number, glyph);
                }

                return glyph;
            }
        }

        /// <summary>Draws a shape where the pen stands, its pen put down first, as a letter and each shape within it start.</summary>
        private void Draw(int number, Pen pen)
        {
            if (!_codes.TryGetValue(number, out byte[] c) || pen.Depth >= MostDepth)
            {
                return;
            }

            pen.Depth++;
            pen.Down = true;
            int i = 0;
            bool skip = false;

            while (i < c.Length && pen.Codes < MostCodes)
            {
                int code = c[i++];
                pen.Codes++;

                if (code == 0)
                {
                    break;
                }

                // A letter cut short within the bytes a code takes is drawn so far.
                if (i + Arguments(code) > c.Length)
                {
                    break;
                }

                if (code > 15)
                {
                    if (!skip)
                    {
                        int length = code >> 4;
                        pen.Line(StepX[code & 15] * length, StepY[code & 15] * length);
                    }
                }
                else if (code == 1)
                {
                    if (!skip)
                    {
                        // The pen put down marks the point it stands on.
                        pen.Down = true;
                        pen.Ink(pen.X, pen.Y);
                    }
                }
                else if (code == 2)
                {
                    pen.Down = skip && pen.Down;
                }
                else if (code == 3 || code == 4)
                {
                    int factor = c[i++];

                    if (!skip && factor != 0)
                    {
                        pen.Scale = code == 3 ? pen.Scale / factor : pen.Scale * factor;
                    }
                }
                else if (code == 5)
                {
                    if (!skip && pen.Stack.Count < 64)
                    {
                        pen.Stack.Push(new KeyValuePair<double, double>(pen.X, pen.Y));
                    }
                }
                else if (code == 6)
                {
                    if (!skip && pen.Stack.Count > 0)
                    {
                        KeyValuePair<double, double> was = pen.Stack.Pop();
                        pen.X = was.Key;
                        pen.Y = was.Value;
                    }
                }
                else if (code == 7)
                {
                    int sub;

                    if (IsUnifont)
                    {
                        // Big-endian in a unifont, as AutoCAD reads it.
                        sub = (c[i] << 8) | c[i + 1];
                        i += 2;
                    }
                    else
                    {
                        sub = c[i++];

                        // A big font's longer form, a number of two bytes and the box it is drawn in, passed over to the number.
                        if (sub == 0)
                        {
                            if (i + 6 > c.Length)
                            {
                                break;
                            }

                            sub = (c[i] << 8) | c[i + 1];
                            i += 6;
                        }
                    }

                    if (!skip)
                    {
                        Draw(sub, pen);
                    }
                }
                else if (code == 8)
                {
                    int dx = (sbyte)c[i];
                    int dy = (sbyte)c[i + 1];
                    i += 2;

                    if (!skip)
                    {
                        pen.Line(dx, dy);
                    }
                }
                else if (code == 9 || code == 13)
                {
                    // A list of moves, or of arcs by their bulge, up to (0, 0).
                    while (true)
                    {
                        if (i + 2 > c.Length)
                        {
                            i = c.Length;
                            break;
                        }

                        int dx = (sbyte)c[i];
                        int dy = (sbyte)c[i + 1];
                        i += 2;

                        if (dx == 0 && dy == 0)
                        {
                            break;
                        }

                        if (code == 9)
                        {
                            if (!skip)
                            {
                                pen.Line(dx, dy);
                            }

                            continue;
                        }

                        if (i + 1 > c.Length)
                        {
                            i = c.Length;
                            break;
                        }

                        int bulge = (sbyte)c[i++];

                        if (!skip)
                        {
                            pen.Bulge(dx, dy, bulge / 127.0);
                        }
                    }
                }
                else if (code == 10)
                {
                    int radius = c[i];
                    int octant = Octant(c[i + 1]);
                    i += 2;

                    if (!skip)
                    {
                        int size = Math.Abs(octant);
                        int count = size & 7;
                        double r = radius * pen.Scale;
                        double start = ((size >> 4) & 7) * Math.PI / 4.0;
                        pen.Arc(r, start, (octant >= 0 ? 1 : -1) * (count == 0 ? 8 : count) * Math.PI / 4.0);
                    }
                }
                else if (code == 11)
                {
                    int startOffset = c[i];
                    int endOffset = c[i + 1];
                    int radius = (c[i + 2] << 8) + c[i + 3];
                    int octant = Octant(c[i + 4]);
                    i += 5;

                    if (!skip)
                    {
                        // The angles in whole degrees, as AutoCAD boxes them (check F1a).
                        int size = Math.Abs(octant);
                        int first = (size >> 4) & 7;
                        int count = size & 7;
                        double part = 45.0 / 256.0;
                        double ends = endOffset == 0 ? 256 : endOffset;
                        double start;
                        double stop;

                        if (octant >= 0)
                        {
                            start = (first * 45.0) + (startOffset * part);
                            stop = ((first + count - 1) * 45.0) + (ends * part);
                        }
                        else
                        {
                            start = (first * 45.0) - (startOffset * part);
                            stop = ((first - count + 1) * 45.0) - (ends * part);
                        }

                        start = Math.Round(start);
                        stop = Math.Round(stop);
                        pen.Arc(radius * pen.Scale, start * Math.PI / 180.0, (stop - start) * Math.PI / 180.0);
                    }
                }
                else if (code == 12)
                {
                    int dx = (sbyte)c[i];
                    int dy = (sbyte)c[i + 1];
                    int bulge = (sbyte)c[i + 2];
                    i += 3;

                    if (!skip)
                    {
                        pen.Bulge(dx, dy, bulge / 127.0);
                    }
                }
                else if (code == 14)
                {
                    skip = true;
                    continue;
                }

                skip = false;
            }

            pen.Depth--;
        }

        /// <summary>How many bytes a code takes after it, those of a list (9 and 13) and of a big font's shape within (7 of 0) read as they come.</summary>
        private int Arguments(int code)
        {
            switch (code)
            {
                case 3:
                case 4:
                    return 1;
                case 7:
                    return IsUnifont ? 2 : 1;
                case 8:
                case 10:
                    return 2;
                case 11:
                    return 5;
                case 12:
                    return 3;
                default:
                    return 0;
            }
        }

        /// <summary>The octants of an arc, a byte of a sign and a size: its high bit clockwise, its low seven the first octant (high half) and their count.</summary>
        private static int Octant(byte value) => (value & 0x80) != 0 ? -(value & 0x7F) : value;

        /// <summary>The pen drawing a letter: where it stands, whether it is down, its scale, its pushed places and the points it has inked.</summary>
        private sealed class Pen
        {
            internal double X { get; set; }

            internal double Y { get; set; }

            internal bool Down { get; set; } = true;

            internal double Scale { get; set; } = 1.0;

            internal int Depth { get; set; }

            internal int Codes { get; set; }

            internal Stack<KeyValuePair<double, double>> Stack { get; } = new Stack<KeyValuePair<double, double>>();

            internal List<double> Xs { get; } = new List<double>();

            internal List<double> Ys { get; } = new List<double>();

            internal void Ink(double x, double y)
            {
                Xs.Add(x);
                Ys.Add(y);
            }

            /// <summary>A move by a vector of the font's units, scaled; inked at both ends where the pen is down.</summary>
            internal void Line(double dx, double dy)
            {
                double x0 = X;
                double y0 = Y;
                X += dx * Scale;
                Y += dy * Scale;

                if (Down)
                {
                    Ink(x0, y0);
                    Ink(X, Y);
                }
            }

            /// <summary>
            /// An arc from where the pen stands, of a radius, from the angle it stands at on its circle round by a sweep (counter-
            /// clockwise above nought); inked as AutoCAD boxes one where the pen is down: up to half a turn the corners of its chord
            /// and of the chord moved by its height, beyond it the corners of its whole circle's box.
            /// </summary>
            internal void Arc(double r, double start, double sweep)
            {
                double cx = X - (r * Math.Cos(start));
                double cy = Y - (r * Math.Sin(start));
                double stop = start + sweep;

                if (Down)
                {
                    if (Math.Abs(sweep) < Math.PI + 1E-9)
                    {
                        double x0 = cx + (r * Math.Cos(start));
                        double y0 = cy + (r * Math.Sin(start));
                        double x1 = cx + (r * Math.Cos(stop));
                        double y1 = cy + (r * Math.Sin(stop));
                        double middle = start + (sweep / 2.0);
                        double sx = cx + (r * Math.Cos(middle)) - ((x0 + x1) / 2.0);
                        double sy = cy + (r * Math.Sin(middle)) - ((y0 + y1) / 2.0);
                        Ink(x0, y0);
                        Ink(x1, y1);
                        Ink(x0 + sx, y0 + sy);
                        Ink(x1 + sx, y1 + sy);
                    }
                    else
                    {
                        Ink(cx - r, cy - r);
                        Ink(cx + r, cy + r);
                        Ink(cx - r, cy + r);
                        Ink(cx + r, cy - r);
                    }
                }

                X = cx + (r * Math.Cos(stop));
                Y = cy + (r * Math.Sin(stop));
            }

            /// <summary>An arc to a point a vector of the font's units away, by its bulge, the tangent of a quarter of its sweep; a straight move where it has none or the pen is up.</summary>
            internal void Bulge(double dx, double dy, double bulge)
            {
                double x1 = X + (dx * Scale);
                double y1 = Y + (dy * Scale);
                double chord = Math.Sqrt(((x1 - X) * (x1 - X)) + ((y1 - Y) * (y1 - Y)));

                if (bulge == 0.0 || !Down || !(chord > 0.0))
                {
                    Line(dx, dy);
                    return;
                }

                double sweep = 4.0 * Math.Atan(bulge);
                double r = chord / (2.0 * Math.Sin(Math.Abs(sweep) / 2.0));
                double nx = -(y1 - Y) / chord;
                double ny = (x1 - X) / chord;
                double h = r * Math.Cos(sweep / 2.0);
                double side = sweep > 0.0 ? 1.0 : -1.0;
                double cx = ((X + x1) / 2.0) + (side * nx * h);
                double cy = ((Y + y1) / 2.0) + (side * ny * h);
                Arc(r, Math.Atan2(Y - cy, X - cx), sweep);
                X = x1;
                Y = y1;
            }
        }
    }
}
