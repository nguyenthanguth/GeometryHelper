using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The form of a compiled SHX file, as its first line names it.</summary>
    public enum ShxFontKind
    {
        /// <summary><c>AutoCAD-86 shapes 1.0</c> or <c>1.1</c>: letters by their byte, shape 0 the font's name and size.</summary>
        Shapes,

        /// <summary><c>AutoCAD-86 unifont 1.0</c>: letters by their Unicode number.</summary>
        Unifont,

        /// <summary><c>AutoCAD-86 bigfont 1.0</c>: letters of two bytes of a code page, drawn beside a style's font.</summary>
        BigFont,
    }

    /// <summary>
    /// A letter of an SHX font drawn by its pen: the strokes it inks, each a polyline of x, y pairs in the font's units, its
    /// arcs cut into chords of at most 11.25 degrees, and how far it moves the pen along the baseline for the next letter.
    /// </summary>
    public sealed class ShxGlyph
    {
        internal ShxGlyph(double advance, IReadOnlyList<double[]> strokes)
            : this(advance, strokes, Array.Empty<double>()) { }

        internal ShxGlyph(double advance, IReadOnlyList<double[]> strokes, IReadOnlyList<double> reach)
        {
            Advance = advance;
            Strokes = strokes;
            Reach = reach;
        }

        /// <summary>Gets how far the letter moves the pen along the baseline, in the font's units: 6 for the A of txt.shx.</summary>
        public double Advance { get; }

        /// <summary>Gets the strokes, each a polyline of x, y pairs in the font's units from the letter's start on the baseline; empty for a space.</summary>
        public IReadOnlyList<double[]> Strokes { get; }

        /// <summary>
        /// The points, x, y pairs in the font's units, that AutoCAD 2021 takes the letter's arcs to reach when it measures its
        /// ink, though nothing is drawn there: an arc under a half circle reaches the rectangle of its chord and of the line
        /// along it touching the arc, a longer one the square about its circle. isocp.shx's 3 is 0.4 of the height wide by its
        /// strokes and 0.441421 by these, as AutoCAD measures it.
        /// </summary>
        internal IReadOnlyList<double> Reach { get; }
    }

    /// <summary>
    /// A compiled SHX font read from the bytes of its file, of the three forms AutoCAD writes (<see cref="ShxFontKind"/>), its
    /// letters drawn by their pen codes into strokes as AutoCAD draws them: romans.shx's capitals reach <see cref="Above"/> 21
    /// units above the baseline, so that a text 2.5 high draws them at 2.5 / 21 of a millimetre to the unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pen codes are those of AutoCAD's documentation of shape files, read as the library's own measuring of SHX fonts
    /// reads them: a byte of 0x10 and over a move of its high half's length along one of 16 directions; 0 the end, 1 the pen
    /// down, 2 up, 3 and 4 the scale divided or multiplied by the next byte, 5 and 6 the pen's place pushed and popped, 7 a
    /// shape drawn within (its number one byte, two big-endian in a unifont, <c>0 hi lo</c> in a big font, and
    /// <c>0 hi lo x y width height</c> in an extended big font, drawn from a base point stretched into a box), 8 and 9 moves
    /// by signed bytes, 10 an arc of octants (a byte of a sign and a size), 11 an arc of fractions of octants (its angles in
    /// whole degrees), 12 and 13 arcs by their bulge (127 a half circle) and 14 the next code for vertical text alone, passed
    /// over as text is drawn across. A letter and each shape within it start with the pen down.
    /// </para>
    /// <para>
    /// A file of another form, one cut short, or a font of no height above its baseline is no font: <see cref="Read"/> gives
    /// null and never throws. A letter that draws shapes within itself over and over stops 16 deep, or after 100 000 codes.
    /// </para>
    /// </remarks>
    public sealed class ShxFont
    {
        private static readonly double[] StepX = { 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5, 0, 0.5, 1, 1 };
        private static readonly double[] StepY = { 0, 0.5, 1, 1, 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5 };
        private const int MostDepth = 16;
        private const int MostCodes = 100000;
        private const double ChordAngle = Math.PI / 16.0;

        private readonly Dictionary<int, byte[]> _codes;
        private readonly Dictionary<int, ShxGlyph> _glyphs = new Dictionary<int, ShxGlyph>();
        private readonly int[] _leads;
        private readonly int _cellWidth;
        private readonly int _cellHeight;

        private ShxFont(
            ShxFontKind kind,
            int above,
            int below,
            Dictionary<int, byte[]> codes,
            int[] leads,
            int cellWidth,
            int cellHeight
        )
        {
            Kind = kind;

            // A big font whose shape 0 gives no height above is drawn 8 to its height, as AutoCAD draws gbcbig.shx.
            Above = kind == ShxFontKind.BigFont && above == 0 ? 8 : above;
            Below = below;
            _codes = codes;
            _leads = leads ?? Array.Empty<int>();
            _cellWidth = cellWidth;
            _cellHeight = cellHeight;
        }

        /// <summary>Gets the form of the file.</summary>
        public ShxFontKind Kind { get; }

        /// <summary>Gets how far the capitals reach above the baseline in the font's units, the height of a text: 6 for txt.shx, 21 for romans.shx.</summary>
        public double Above { get; }

        /// <summary>Gets how far the letters reach below the baseline in the font's units: 2 for txt.shx, 7 for romans.shx.</summary>
        public double Below { get; }

        /// <summary>Gets the number of letters and shapes the font holds, shape 0 left out.</summary>
        public int Count => _codes.Count;

        /// <summary>
        /// Reads a font from the bytes of a compiled SHX file; null where they are no font of the forms of
        /// <see cref="ShxFontKind"/>, are cut short, or give a font of letters no height above its baseline.
        /// </summary>
        /// <param name="bytes">The bytes of the file.</param>
        /// <returns>The font, or null.</returns>
        public static ShxFont Read(byte[] bytes)
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

            try
            {
                if (
                    header.StartsWith("AutoCAD-86 shapes 1.0", StringComparison.Ordinal)
                    || header.StartsWith("AutoCAD-86 shapes 1.1", StringComparison.Ordinal)
                )
                {
                    return Shapes(bytes, end + 1);
                }

                if (header.StartsWith("AutoCAD-86 unifont 1.0", StringComparison.Ordinal))
                {
                    return Unifont(bytes, end + 1);
                }

                return header.StartsWith("AutoCAD-86 bigfont 1.0", StringComparison.Ordinal)
                    ? Big(bytes, end + 1)
                    : null;
            }
            catch (Exception e) when (e is IndexOutOfRangeException || e is ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Gets whether the font has a letter or shape of a number: a byte in a font of shapes, a Unicode number in a unifont, two bytes in a big font.</summary>
        /// <param name="number">The number.</param>
        /// <returns>True where it has.</returns>
        public bool Has(int number) => number != 0 && _codes.ContainsKey(number);

        /// <summary>Gets whether a byte opens a letter of two bytes in a big font, by its ranges: 0xD6 in gbcbig.shx; false in a font of another form.</summary>
        /// <param name="lead">The byte.</param>
        /// <returns>True where it opens one.</returns>
        public bool Opens(int lead)
        {
            for (int i = 0; i + 1 < _leads.Length; i += 2)
            {
                if (lead >= _leads[i] && lead <= _leads[i + 1])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gets a letter of the font drawn into its strokes, drawn once and kept; null where the font has none of that number.</summary>
        /// <param name="number">The letter's number, as <see cref="Has"/> takes it.</param>
        /// <returns>The letter, or null.</returns>
        public ShxGlyph GlyphOf(int number)
        {
            if (!Has(number))
            {
                return null;
            }

            lock (_glyphs)
            {
                if (!_glyphs.TryGetValue(number, out ShxGlyph glyph))
                {
                    var pen = new Pen();
                    Draw(number, pen);
                    pen.Lift();
                    glyph = new ShxGlyph(pen.X, pen.Strokes.ToArray(), pen.Reach.ToArray());
                    _glyphs.Add(number, glyph);
                }

                return glyph;
            }
        }

        private static ShxFont Shapes(byte[] bytes, int at)
        {
            int count = U16(bytes, at + 4);
            at += 6;
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

            return above > 0 ? new ShxFont(ShxFontKind.Shapes, above, below, codes, null, 0, 0) : null;
        }

        private static ShxFont Unifont(byte[] bytes, int at)
        {
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

            return above > 0 ? new ShxFont(ShxFontKind.Unifont, above, below, codes, null, 0, 0) : null;
        }

        private static ShxFont Big(byte[] bytes, int at)
        {
            int count = U16(bytes, at + 2);
            int ranges = U16(bytes, at + 4);
            at += 6;

            if (at + (ranges * 4) + ((long)count * 8) > bytes.Length)
            {
                return null;
            }

            var leads = new int[ranges * 2];

            for (int i = 0; i < leads.Length; i++)
            {
                leads[i] = U16(bytes, at + (i * 2));
            }

            at += ranges * 4;
            var codes = new Dictionary<int, byte[]>();
            byte[] head = null;

            for (int i = 0; i < count; i++)
            {
                int entry = at + (i * 8);
                int number = U16(bytes, entry);
                int length = U16(bytes, entry + 2);
                long place = (uint)(
                    bytes[entry + 4] | (bytes[entry + 5] << 8) | (bytes[entry + 6] << 16) | (bytes[entry + 7] << 24)
                );

                if (length == 0 && place == 0)
                {
                    continue;
                }

                if (place + length > bytes.Length)
                {
                    return null;
                }

                int zero = Array.IndexOf(bytes, (byte)0, (int)place, length);

                if (zero < 0)
                {
                    continue;
                }

                if (number == 0)
                {
                    head = head ?? Slice(bytes, zero + 1, (int)place + length);
                }
                else if (!codes.ContainsKey(number))
                {
                    codes.Add(number, Slice(bytes, zero + 1, (int)place + length));
                }
            }

            if (head == null || head.Length < 2)
            {
                return null;
            }

            // An extended big font gives five bytes: its height, 0, its modes, the width of its cell and 0.
            bool extended = head.Length >= 5;
            return new ShxFont(
                ShxFontKind.BigFont,
                head[0],
                extended ? 0 : head[1],
                codes,
                leads,
                extended ? head[3] : 0,
                extended ? head[0] : 0
            );
        }

        private static int U16(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

        private static byte[] Slice(byte[] bytes, int from, int to)
        {
            var slice = new byte[Math.Max(0, to - from)];
            Array.Copy(bytes, from, slice, 0, slice.Length);
            return slice;
        }

        private static int Octant(byte value) => (value & 0x80) != 0 ? -(value & 0x7F) : value;

        /// <summary>How many bytes a code takes after it, the lists of 9 and 13 and a big font's long shape within read as they come.</summary>
        private int Arguments(int code)
        {
            switch (code)
            {
                case 3:
                case 4:
                    return 1;
                case 7:
                    return Kind == ShxFontKind.Unifont ? 2 : 1;
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

        /// <summary>Draws a shape where the pen stands, its pen put down first.</summary>
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

                if (code == 0 || i + Arguments(code) > c.Length)
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
                else if (code == 1 || code == 2)
                {
                    if (!skip)
                    {
                        pen.Down = code == 1;

                        if (code == 2)
                        {
                            pen.Lift();
                        }
                        else
                        {
                            // Where the pen is put down counts for AutoCAD's measure though it moves no further, as the
                            // foot of monotxt.shx's 1 does.
                            pen.Reach.Add(pen.X);
                            pen.Reach.Add(pen.Y);
                        }
                    }
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
                        pen.Lift();
                        pen.X = was.Key;
                        pen.Y = was.Value;
                    }
                }
                else if (code == 7)
                {
                    int sub;

                    if (Kind == ShxFontKind.Unifont)
                    {
                        sub = (c[i] << 8) | c[i + 1];
                        i += 2;
                    }
                    else
                    {
                        sub = c[i++];

                        if (sub == 0)
                        {
                            // A shape of two bytes; in an extended big font, and passed over in a font of shapes, with the base
                            // point and box it is drawn in.
                            bool boxed = Kind != ShxFontKind.BigFont || _cellWidth > 0;
                            int more = boxed ? 6 : 2;

                            if (i + more > c.Length)
                            {
                                break;
                            }

                            sub = (c[i] << 8) | c[i + 1];

                            if (!skip && boxed && Kind == ShxFontKind.BigFont)
                            {
                                Stretched(sub, pen, (sbyte)c[i + 2], (sbyte)c[i + 3], c[i + 4], c[i + 5]);
                                sub = 0;
                            }

                            i += more;
                        }
                    }

                    if (!skip && sub != 0)
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
                    while (i + 2 <= c.Length)
                    {
                        int dx = (sbyte)c[i];
                        int dy = (sbyte)c[i + 1];
                        i += 2;

                        if (dx == 0 && dy == 0)
                        {
                            break;
                        }

                        int bulge = 0;

                        if (code == 13)
                        {
                            if (i >= c.Length)
                            {
                                break;
                            }

                            bulge = (sbyte)c[i++];
                        }

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
                    int size = Math.Abs(octant);
                    int count = size & 7;

                    // A big font's whole circle is not drawn, as AutoCAD draws chineset.shx's full stop as its dot alone.
                    if (!skip && (Kind != ShxFontKind.BigFont || count != 0))
                    {
                        double start = ((size >> 4) & 7) * Math.PI / 4.0;
                        pen.Arc(
                            radius * pen.Scale,
                            start,
                            (octant >= 0 ? 1 : -1) * (count == 0 ? 8 : count) * Math.PI / 4.0
                        );
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
                        int size = Math.Abs(octant);
                        int first = (size >> 4) & 7;
                        int count = size & 7;
                        double part = 45.0 / 256.0;
                        double ends = endOffset == 0 ? 256 : endOffset;
                        double start =
                            octant >= 0 ? (first * 45.0) + (startOffset * part) : (first * 45.0) - (startOffset * part);
                        double stop =
                            octant >= 0
                                ? ((first + count - 1) * 45.0) + (ends * part)
                                : ((first - count + 1) * 45.0) - (ends * part);
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

        /// <summary>
        /// Draws a shape of an extended big font as <c>7 0 hi lo x y width height</c> asks: from the pen moved by the base
        /// point, the font's cell stretched to the box, then the pen put back across where it stood and moved up as the shape
        /// moved it from the base point.
        /// </summary>
        private void Stretched(int number, Pen pen, int x, int y, int width, int height)
        {
            if (_cellWidth == 0 || _cellHeight == 0)
            {
                return;
            }

            double x0 = pen.X;
            double y0 = pen.Y;
            double across = pen.Across;
            double upward = pen.Upward;
            pen.Lift();
            pen.X = x0 + (x * pen.Scale * across);
            pen.Y = y0 + (y * pen.Scale * upward);
            double baseY = pen.Y;
            pen.Across = across * width / _cellWidth;
            pen.Upward = upward * height / _cellHeight;
            Draw(number, pen);
            pen.Lift();
            pen.Across = across;
            pen.Upward = upward;
            pen.Y = y0 + (pen.Y - baseY);
            pen.X = x0;
        }

        /// <summary>The pen drawing a letter: where it stands, whether it is down, its scale and stretch, its pushed places and the strokes it has drawn.</summary>
        private sealed class Pen
        {
            private List<double> _stroke;

            internal double X { get; set; }

            internal double Y { get; set; }

            internal bool Down { get; set; } = true;

            internal double Scale { get; set; } = 1.0;

            internal double Across { get; set; } = 1.0;

            internal double Upward { get; set; } = 1.0;

            internal int Depth { get; set; }

            internal int Codes { get; set; }

            internal Stack<KeyValuePair<double, double>> Stack { get; } = new Stack<KeyValuePair<double, double>>();

            internal List<double[]> Strokes { get; } = new List<double[]>();

            internal List<double> Reach { get; } = new List<double>();

            /// <summary>Ends the stroke being drawn, kept where it has two points or more.</summary>
            internal void Lift()
            {
                if (_stroke != null && _stroke.Count >= 4)
                {
                    Strokes.Add(_stroke.ToArray());
                }

                _stroke = null;
            }

            /// <summary>Moves the pen to a point, inking the way there where it is down: the stroke drawn goes on, else one begins where the pen stood.</summary>
            internal void To(double x, double y)
            {
                if (Down)
                {
                    if (_stroke == null)
                    {
                        _stroke = new List<double> { X, Y };
                    }

                    _stroke.Add(x);
                    _stroke.Add(y);
                }
                else
                {
                    Lift();
                }

                X = x;
                Y = y;
            }

            /// <summary>A move by a vector of the font's units, scaled and stretched.</summary>
            internal void Line(double dx, double dy) => To(X + (dx * Scale * Across), Y + (dy * Scale * Upward));

            /// <summary>An arc from where the pen stands, of a radius already scaled, from the angle it stands at on its circle round by a sweep, counter-clockwise above nought.</summary>
            internal void Arc(double r, double start, double sweep)
            {
                int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / ChordAngle - 1E-9));
                double x0 = X;
                double y0 = Y;
                double sx = r * Math.Cos(start);
                double sy = r * Math.Sin(start);

                if (Down)
                {
                    Reaches(r, start, sweep);
                }

                for (int k = 1; k <= n; k++)
                {
                    double a = start + (sweep * k / n);
                    To(x0 + (((r * Math.Cos(a)) - sx) * Across), y0 + (((r * Math.Sin(a)) - sy) * Upward));
                }
            }

            /// <summary>
            /// Keeps the points AutoCAD takes an arc from where the pen stands to reach when it measures ink: the corners of
            /// the rectangle of its chord and of the line along it touching the arc's middle where it turns less than a half
            /// circle, the corners of the square about its circle where it turns more. A half circle is measured by its
            /// rectangle where, taken counter-clockwise, it starts on the X axis or below it, and by its square where it starts
            /// above, as AutoCAD measures the 16 half circles of octant arcs.
            /// </summary>
            private void Reaches(double r, double start, double sweep)
            {
                double turn = Math.Abs(sweep);
                double first = sweep >= 0.0 ? start : start + sweep;
                double from = first - (2.0 * Math.PI * Math.Floor(first / (2.0 * Math.PI)));
                bool whole = turn > Math.PI + 1E-9 || (turn > Math.PI - 1E-9 && from > 1E-9 && from < Math.PI - 1E-9);
                double cx = X - (r * Math.Cos(start) * Across);
                double cy = Y - (r * Math.Sin(start) * Upward);

                if (whole)
                {
                    Reach.Add(cx - (r * Across));
                    Reach.Add(cy - (r * Upward));
                    Reach.Add(cx + (r * Across));
                    Reach.Add(cy + (r * Upward));
                    return;
                }

                double rise = r * (1.0 - Math.Cos(turn / 2.0));
                double middle = start + (sweep / 2.0);
                double ux = rise * Math.Cos(middle);
                double uy = rise * Math.Sin(middle);
                double end = start + sweep;
                Reach.Add(X + (ux * Across));
                Reach.Add(Y + (uy * Upward));
                Reach.Add(cx + (((r * Math.Cos(end)) + ux) * Across));
                Reach.Add(cy + (((r * Math.Sin(end)) + uy) * Upward));
            }

            /// <summary>An arc to a point a vector of the font's units away, by its bulge, the tangent of a quarter of its sweep; a straight move where it has none.</summary>
            internal void Bulge(double dx, double dy, double bulge)
            {
                double ux = dx * Scale;
                double uy = dy * Scale;
                double chord = Math.Sqrt((ux * ux) + (uy * uy));

                if (bulge == 0.0 || !(chord > 0.0))
                {
                    Line(dx, dy);
                    return;
                }

                double sweep = 4.0 * Math.Atan(bulge);
                double r = chord / (2.0 * Math.Sin(Math.Abs(sweep) / 2.0));
                double h = r * Math.Cos(sweep / 2.0) * (sweep > 0.0 ? 1.0 : -1.0);

                // The centre from the chord's middle, along its left normal for an arc counter-clockwise.
                double cx = (ux / 2.0) - (uy / chord * h);
                double cy = (uy / 2.0) + (ux / chord * h);
                Arc(r, Math.Atan2(-cy, -cx), sweep);
            }
        }
    }
}
