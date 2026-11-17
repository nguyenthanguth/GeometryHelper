using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The outlines of an embedded TrueType font of a PDF, by glyph number. The fonts a PDF embeds are subsets with the tables
    /// they need and often no <c>cmap</c> or <c>name</c>, so this reads only <c>head</c>, <c>maxp</c>, <c>hhea</c>,
    /// <c>hmtx</c>, <c>loca</c>, <c>glyf</c> and, where it is there, <c>cmap</c> (formats 0, 4, 6 and 12). Every read is bounded by
    /// the bytes; a font that is cut short gives no outline, and nothing throws.
    /// </summary>
    internal sealed class PdfTrueType
    {
        private const int CurvePieces = 5;
        private const int DeepestComposite = 8;

        private readonly byte[] _bytes;
        private readonly int _glyf;
        private readonly int _glyfEnd;
        private readonly int _loca;
        private readonly int _locaEnd;
        private readonly bool _longLoca;
        private readonly int _hmtx;
        private readonly int _hmtxEnd;
        private readonly int _metrics;
        private readonly Dictionary<int, List<double[]>> _outlines = new Dictionary<int, List<double[]>>();
        private readonly List<KeyValuePair<int, int>> _maps = new List<KeyValuePair<int, int>>();

        private PdfTrueType(
            byte[] bytes,
            int unitsPerEm,
            int glyphs,
            int glyf,
            int glyfEnd,
            int loca,
            int locaEnd,
            bool longLoca,
            int hmtx,
            int hmtxEnd,
            int metrics
        )
        {
            _bytes = bytes;
            UnitsPerEm = unitsPerEm;
            GlyphCount = glyphs;
            _glyf = glyf;
            _glyfEnd = glyfEnd;
            _loca = loca;
            _locaEnd = locaEnd;
            _longLoca = longLoca;
            _hmtx = hmtx;
            _hmtxEnd = hmtxEnd;
            _metrics = metrics;
        }

        /// <summary>Gets the font's units to an em.</summary>
        public int UnitsPerEm { get; }

        /// <summary>Gets how many glyphs the font holds.</summary>
        public int GlyphCount { get; }

        /// <summary>Gets whether the font has a table mapping characters to glyphs.</summary>
        public bool HasMap => _maps.Count > 0;

        /// <summary>Reads a font from the bytes of its file; null where they are no TrueType font of outlines.</summary>
        public static PdfTrueType Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12)
            {
                return null;
            }

            int at = 0;

            // A collection: the first font of it.
            if (U32(bytes, 0) == 0x74746366 && bytes.Length >= 16)
            {
                at = (int)U32(bytes, 12);
            }

            uint version = U32(bytes, at);

            if (version != 0x00010000 && version != 0x74727565)
            {
                return null;
            }

            int count = U16(bytes, at + 4);
            var tables = new Dictionary<string, int[]>();

            for (int i = 0; i < count; i++)
            {
                int entry = at + 12 + 16 * i;

                if (entry + 16 > bytes.Length)
                {
                    break;
                }

                string tag =
                    "" + (char)bytes[entry] + (char)bytes[entry + 1] + (char)bytes[entry + 2] + (char)bytes[entry + 3];
                long offset = U32(bytes, entry + 8);
                long length = U32(bytes, entry + 12);

                if (offset < bytes.Length)
                {
                    tables[tag] = new[] { (int)offset, (int)Math.Min(length, bytes.Length - offset) };
                }
            }

            if (
                !tables.TryGetValue("head", out int[] head)
                || head[1] < 54
                || !tables.TryGetValue("maxp", out int[] maxp)
                || maxp[1] < 6
                || !tables.TryGetValue("loca", out int[] loca)
                || !tables.TryGetValue("glyf", out int[] glyf)
            )
            {
                return null;
            }

            int unitsPerEm = U16(bytes, head[0] + 18);
            int form = U16(bytes, head[0] + 50);
            int glyphs = U16(bytes, maxp[0] + 4);
            int hmtx = 0;
            int hmtxEnd = 0;
            int metrics = 0;

            if (tables.TryGetValue("hhea", out int[] hhea) && hhea[1] >= 36 && tables.TryGetValue("hmtx", out int[] hm))
            {
                metrics = U16(bytes, hhea[0] + 34);
                hmtx = hm[0];
                hmtxEnd = hm[0] + hm[1];
            }

            if (unitsPerEm < 16 || unitsPerEm > 16384 || glyphs == 0)
            {
                return null;
            }

            var font = new PdfTrueType(
                bytes,
                unitsPerEm,
                glyphs,
                glyf[0],
                glyf[0] + glyf[1],
                loca[0],
                loca[0] + loca[1],
                form == 1,
                hmtx,
                hmtxEnd,
                metrics
            );

            if (tables.TryGetValue("cmap", out int[] cmap) && cmap[1] >= 4)
            {
                font.ReadMaps(cmap[0], cmap[0] + cmap[1]);
            }

            return font;
        }

        /// <summary>The advance of a glyph in font units; nought where the font gives none.</summary>
        public double Advance(int glyph)
        {
            if (_metrics <= 0 || glyph < 0)
            {
                return 0.0;
            }

            int n = Math.Min(glyph, _metrics - 1);
            int at = _hmtx + 4 * n;
            return at + 2 <= _hmtxEnd ? U16(_bytes, at) : 0.0;
        }

        /// <summary>
        /// The outline of a glyph, each contour as x and y in turn in font units, y up, closed implicitly, the curves cut into
        /// short pieces; empty for a glyph with no ink, null for one the font does not hold.
        /// </summary>
        public IReadOnlyList<double[]> Outline(int glyph)
        {
            if (glyph < 0 || glyph >= GlyphCount)
            {
                return null;
            }

            if (_outlines.TryGetValue(glyph, out List<double[]> cached))
            {
                return cached;
            }

            var contours = new List<double[]>();
            Add(glyph, contours, 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0);
            _outlines[glyph] = contours;
            return contours;
        }

        /// <summary>The glyph a Unicode number maps to in the font's Unicode table; nought where there is none.</summary>
        public int GlyphOfUnicode(int number) =>
            Lookup(3, 1, number) is int a && a > 0 ? a
            : Lookup(3, 10, number) is int b && b > 0 ? b
            : Lookup(0, -1, number) is int c && c > 0 ? c
            : 0;

        /// <summary>The glyph a character code maps to in the font's symbol or Mac table; nought where there is none.</summary>
        public int GlyphOfCode(int code)
        {
            foreach (int c in new[] { code, 0xF000 + code, 0xF100 + code, 0xF200 + code })
            {
                if (Lookup(3, 0, c) is int a && a > 0)
                {
                    return a;
                }
            }

            return Lookup(1, 0, code) is int m && m > 0 ? m : 0;
        }

        private int? Lookup(int platform, int encoding, int code)
        {
            foreach (KeyValuePair<int, int> map in _maps)
            {
                int p = map.Key >> 16;
                int e = map.Key & 0xFFFF;

                if (p != platform || (encoding >= 0 && e != encoding))
                {
                    continue;
                }

                int glyph = Map(map.Value, code);

                if (glyph > 0)
                {
                    return glyph;
                }
            }

            return null;
        }

        private void ReadMaps(int start, int end)
        {
            int count = U16(_bytes, start + 2);

            for (int i = 0; i < count && start + 4 + 8 * i + 8 <= end; i++)
            {
                int record = start + 4 + 8 * i;
                int platform = U16(_bytes, record);
                int encoding = U16(_bytes, record + 2);
                long offset = U32(_bytes, record + 4);

                if (start + offset + 4 <= end)
                {
                    _maps.Add(new KeyValuePair<int, int>((platform << 16) | encoding, (int)(start + offset)));
                }
            }
        }

        private int Map(int at, int code)
        {
            int format = U16(_bytes, at);

            switch (format)
            {
                case 0:
                    return code >= 0 && code < 256 && at + 6 + code < _bytes.Length ? _bytes[at + 6 + code] : 0;
                case 4:
                {
                    int segments = U16(_bytes, at + 6) / 2;
                    int ends = at + 14;
                    int starts = ends + 2 * segments + 2;
                    int deltas = starts + 2 * segments;
                    int offsets = deltas + 2 * segments;

                    if (offsets + 2 * segments > _bytes.Length || code > 0xFFFF)
                    {
                        return 0;
                    }

                    for (int s = 0; s < segments; s++)
                    {
                        if (code > U16(_bytes, ends + 2 * s))
                        {
                            continue;
                        }

                        int first = U16(_bytes, starts + 2 * s);

                        if (code < first)
                        {
                            return 0;
                        }

                        int delta = (short)U16(_bytes, deltas + 2 * s);
                        int range = U16(_bytes, offsets + 2 * s);

                        if (range == 0)
                        {
                            return (code + delta) & 0xFFFF;
                        }

                        int place = offsets + 2 * s + range + 2 * (code - first);

                        if (place + 2 > _bytes.Length)
                        {
                            return 0;
                        }

                        int glyph = U16(_bytes, place);
                        return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
                    }

                    return 0;
                }
                case 6:
                {
                    int first = U16(_bytes, at + 6);
                    int n = U16(_bytes, at + 8);
                    return code >= first && code < first + n && at + 10 + 2 * (code - first) + 2 <= _bytes.Length
                        ? U16(_bytes, at + 10 + 2 * (code - first))
                        : 0;
                }
                case 12:
                {
                    long groups = U32(_bytes, at + 12);

                    for (long g = 0; g < groups && at + 16 + 12 * g + 12 <= _bytes.Length; g++)
                    {
                        int record = (int)(at + 16 + 12 * g);
                        long low = U32(_bytes, record);
                        long high = U32(_bytes, record + 4);

                        if (code >= low && code <= high)
                        {
                            return (int)(U32(_bytes, record + 8) + (code - low));
                        }
                    }

                    return 0;
                }
                default:
                    return 0;
            }
        }

        private bool Record(int glyph, out int at, out int end)
        {
            at = 0;
            end = 0;

            if (glyph < 0 || glyph >= GlyphCount)
            {
                return false;
            }

            int step = _longLoca ? 4 : 2;
            int entry = _loca + step * glyph;

            if (entry + 2 * step > _locaEnd)
            {
                return false;
            }

            long from = _longLoca ? U32(_bytes, entry) : 2L * U16(_bytes, entry);
            long to = _longLoca ? U32(_bytes, entry + 4) : 2L * U16(_bytes, entry + 2);

            if (to < from + 10 || _glyf + to > _glyfEnd)
            {
                return false;
            }

            at = _glyf + (int)from;
            end = _glyf + (int)to;
            return true;
        }

        private void Add(
            int glyph,
            List<double[]> contours,
            double a,
            double b,
            double c,
            double d,
            double e,
            double f,
            int depth
        )
        {
            if (depth > DeepestComposite || !Record(glyph, out int at, out int end))
            {
                return;
            }

            int count = (short)U16(_bytes, at);
            int p = at + 10;

            if (count < 0)
            {
                bool more = true;

                while (more && p + 4 <= end)
                {
                    int flags = U16(_bytes, p);
                    int index = U16(_bytes, p + 2);
                    p += 4;
                    double dx;
                    double dy;

                    if ((flags & 1) != 0)
                    {
                        if (p + 4 > end)
                        {
                            return;
                        }

                        dx = (flags & 2) != 0 ? (short)U16(_bytes, p) : 0.0;
                        dy = (flags & 2) != 0 ? (short)U16(_bytes, p + 2) : 0.0;
                        p += 4;
                    }
                    else
                    {
                        if (p + 2 > end)
                        {
                            return;
                        }

                        dx = (flags & 2) != 0 ? (sbyte)_bytes[p] : 0.0;
                        dy = (flags & 2) != 0 ? (sbyte)_bytes[p + 1] : 0.0;
                        p += 2;
                    }

                    double ma = 1.0;
                    double mb = 0.0;
                    double mc = 0.0;
                    double md = 1.0;

                    if ((flags & 8) != 0 && p + 2 <= end)
                    {
                        ma = md = F2Dot14(p);
                        p += 2;
                    }
                    else if ((flags & 0x40) != 0 && p + 4 <= end)
                    {
                        ma = F2Dot14(p);
                        md = F2Dot14(p + 2);
                        p += 4;
                    }
                    else if ((flags & 0x80) != 0 && p + 8 <= end)
                    {
                        ma = F2Dot14(p);
                        mb = F2Dot14(p + 2);
                        mc = F2Dot14(p + 4);
                        md = F2Dot14(p + 6);
                        p += 8;
                    }

                    // The component's matrix, then the one this glyph is placed by.
                    Add(
                        index,
                        contours,
                        a * ma + c * mb,
                        b * ma + d * mb,
                        a * mc + c * md,
                        b * mc + d * md,
                        a * dx + c * dy + e,
                        b * dx + d * dy + f,
                        depth + 1
                    );
                    more = (flags & 0x20) != 0;
                }

                return;
            }

            if (count == 0 || p + 2 * count + 2 > end)
            {
                return;
            }

            var endPoints = new int[count];

            for (int i = 0; i < count; i++)
            {
                endPoints[i] = U16(_bytes, p + 2 * i);
            }

            int points = endPoints[count - 1] + 1;
            p += 2 * count;
            int instructions = U16(_bytes, p);
            p += 2 + instructions;

            if (points <= 0 || points > 20000 || p > end)
            {
                return;
            }

            var flagsOf = new byte[points];

            for (int i = 0; i < points; )
            {
                if (p >= end)
                {
                    return;
                }

                byte flag = _bytes[p++];
                flagsOf[i++] = flag;

                if ((flag & 8) != 0)
                {
                    if (p >= end)
                    {
                        return;
                    }

                    int repeat = _bytes[p++];

                    while (repeat-- > 0 && i < points)
                    {
                        flagsOf[i++] = flag;
                    }
                }
            }

            var xs = new double[points];
            var ys = new double[points];
            int value = 0;

            for (int i = 0; i < points; i++)
            {
                if ((flagsOf[i] & 2) != 0)
                {
                    if (p >= end)
                    {
                        return;
                    }

                    int delta = _bytes[p++];
                    value += (flagsOf[i] & 16) != 0 ? delta : -delta;
                }
                else if ((flagsOf[i] & 16) == 0)
                {
                    if (p + 2 > end)
                    {
                        return;
                    }

                    value += (short)U16(_bytes, p);
                    p += 2;
                }

                xs[i] = value;
            }

            value = 0;

            for (int i = 0; i < points; i++)
            {
                if ((flagsOf[i] & 4) != 0)
                {
                    if (p >= end)
                    {
                        return;
                    }

                    int delta = _bytes[p++];
                    value += (flagsOf[i] & 32) != 0 ? delta : -delta;
                }
                else if ((flagsOf[i] & 32) == 0)
                {
                    if (p + 2 > end)
                    {
                        return;
                    }

                    value += (short)U16(_bytes, p);
                    p += 2;
                }

                ys[i] = value;
            }

            int begin = 0;

            for (int k = 0; k < count; k++)
            {
                int last = endPoints[k];

                if (last >= begin && last < points)
                {
                    Contour(xs, ys, flagsOf, begin, last, contours, a, b, c, d, e, f);
                }

                begin = last + 1;
            }
        }

        private static void Contour(
            double[] xs,
            double[] ys,
            byte[] flags,
            int first,
            int last,
            List<double[]> contours,
            double a,
            double b,
            double c,
            double d,
            double e,
            double f
        )
        {
            int n = last - first + 1;

            if (n < 2)
            {
                return;
            }

            // The contour as on-curve points and the off-curve ones between: a run of off-curve points has an on-curve one
            // implied halfway between each two.
            var px = new List<double>();
            var py = new List<double>();
            var on = new List<bool>();

            for (int i = 0; i < n; i++)
            {
                int at = first + i;
                int next = first + (i + 1) % n;
                px.Add(xs[at]);
                py.Add(ys[at]);
                on.Add((flags[at] & 1) != 0);

                if ((flags[at] & 1) == 0 && (flags[next] & 1) == 0)
                {
                    px.Add((xs[at] + xs[next]) / 2.0);
                    py.Add((ys[at] + ys[next]) / 2.0);
                    on.Add(true);
                }
            }

            int start = on.IndexOf(true);

            if (start < 0)
            {
                return;
            }

            var ring = new List<double>();

            void Point(double x, double y)
            {
                ring.Add(a * x + c * y + e);
                ring.Add(b * x + d * y + f);
            }

            int m = px.Count;
            Point(px[start], py[start]);

            for (int i = 1; i <= m; i++)
            {
                int at = (start + i) % m;
                int prev = (start + i - 1) % m;

                if (on[at])
                {
                    if (i < m)
                    {
                        Point(px[at], py[at]);
                    }
                }
                else
                {
                    int to = (start + i + 1) % m;
                    double x0 = px[prev];
                    double y0 = py[prev];

                    for (int s = 1; s <= CurvePieces; s++)
                    {
                        double t = (double)s / CurvePieces;
                        double u = 1.0 - t;
                        Point(
                            u * u * x0 + 2 * u * t * px[at] + t * t * px[to],
                            u * u * y0 + 2 * u * t * py[at] + t * t * py[to]
                        );
                    }

                    i++;
                }
            }

            // The last point returns to the first: the ring is closed implicitly.
            if (
                ring.Count >= 4
                && Math.Abs(ring[0] - ring[ring.Count - 2]) < 1E-9
                && Math.Abs(ring[1] - ring[ring.Count - 1]) < 1E-9
            )
            {
                ring.RemoveRange(ring.Count - 2, 2);
            }

            if (ring.Count >= 6)
            {
                contours.Add(ring.ToArray());
            }
        }

        private double F2Dot14(int at) => (short)U16(_bytes, at) / 16384.0;

        private static int U16(byte[] b, int at) => at >= 0 && at + 2 <= b.Length ? (b[at] << 8) | b[at + 1] : 0;

        private static uint U32(byte[] b, int at) =>
            at >= 0 && at + 4 <= b.Length
                ? ((uint)b[at] << 24) | ((uint)b[at + 1] << 16) | ((uint)b[at + 2] << 8) | b[at + 3]
                : 0u;
    }
}
