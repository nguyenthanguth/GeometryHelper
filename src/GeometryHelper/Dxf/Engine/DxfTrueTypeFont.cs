using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A TrueType font, of a .ttf or .otf file of glyf outlines or the first face of a .ttc, read as AutoCAD 2021 measures it
    /// (checks F1a and F2): its height the top of its letter A, its depth below the baseline its <c>usWinDescent</c>, each
    /// letter's advance that of its <c>hmtx</c> and its ink the box its <c>glyf</c> header gives, as AutoCAD boxes it
    /// (<see cref="Letter"/>), no pair of letters kerned. Arial's A reaches 1466 units up and is inked from -3 to 1369, so
    /// that "A" 4 high is inked from -0.008 to 3.735, though Arial's capitals are written as 1467 high and its em is 2048.
    /// </summary>
    /// <remarks>
    /// The tables read are the offset table (of TrueType outlines, 0x00010000 or <c>true</c>, or a collection,
    /// <c>ttcf</c>, of which the first face is taken), <c>head</c> (the form of <c>loca</c>), <c>hhea</c> (how many advances
    /// <c>hmtx</c> holds), <c>maxp</c> (how many letters), <c>hmtx</c>, <c>cmap</c> (of Windows: the subtable of format 12 of
    /// every Unicode number, else that of format 4 of the first 65 536, else the symbols' of format 4, a letter's byte read at
    /// U+F000 on), <c>loca</c> and <c>glyf</c> (the header of each letter alone: a letter made of others is boxed by its
    /// own), <c>OS/2</c> (its <c>usWinDescent</c>), and <c>name</c> and <c>head</c> for the face's typeface and whether it is
    /// bold or italic. Every read is bounded by the file and its tables: a file of another form, of CFF outlines
    /// (<c>OTTO</c>), cut short or of no A of ink is no font, and nothing throws.
    /// </remarks>
    internal sealed class DxfTrueTypeFont : DxfFont
    {
        private const uint OfTrueType = 0x00010000;
        private const uint OfTrue = 0x74727565;
        private const uint OfCollection = 0x74746366;
        private const uint OfCff = 0x4F54544F;

        private readonly byte[] _bytes;
        private readonly int _metrics;
        private readonly int _advances;
        private readonly int _letters;
        private readonly int _loca;
        private readonly bool _longLoca;
        private readonly int _glyf;
        private readonly int _glyfEnd;
        private readonly int _map;
        private readonly int _mapEnd;
        private readonly bool _wide;
        private readonly bool _symbol;
        private readonly string[] _families;
        private readonly int _style;
        private readonly Dictionary<int, Glyph> _glyphs = new Dictionary<int, Glyph>();
        private double _above;

        private DxfTrueTypeFont(byte[] bytes, Tables tables)
        {
            _bytes = bytes;
            _metrics = tables.Metrics;
            _advances = tables.Advances;
            _letters = tables.Letters;
            _loca = tables.Loca;
            _longLoca = tables.LongLoca;
            _glyf = tables.Glyf;
            _glyfEnd = tables.GlyfEnd;
            _map = tables.Map;
            _mapEnd = tables.MapEnd;
            _wide = tables.Wide;
            _symbol = tables.Symbol;
            _families = tables.Families;
            _style = tables.Style;
            Below = tables.Descent;
            IsCollection = tables.Collection;
        }

        /// <summary>The top of the font's letter A, in its units, which a text's height stands for: 1466 for Arial, 1387 for Times New Roman.</summary>
        internal override double Above => _above;

        /// <summary>The font's <c>usWinDescent</c>, which a text held at its bottom stands on: 434 for Arial; NaN where the font has no <c>OS/2</c>.</summary>
        internal override double Below { get; }

        /// <summary>Whether the font is the first face of a collection, a .ttc.</summary>
        internal bool IsCollection { get; }

        /// <summary>Whether the font's letters are read from the subtable of symbols of its <c>cmap</c>, as Symbol's and Wingdings'.</summary>
        internal bool IsSymbols => _symbol;

        /// <summary>The names of the face's family, as Windows names it (<c>name</c> 1 of the platform 3), each language's: "Arial" for arial.ttf and arialbd.ttf.</summary>
        internal string TypeFace => _families.Length == 0 ? string.Empty : _families[0];

        /// <summary>
        /// Whether the face is the one a typeface, bold or not and italic or not, names, as AutoCAD 2021 draws a style of that
        /// typeface by it and not by its file (check F2): a name of its family, of any language, and its bold and italic
        /// (<c>head</c>'s <c>macStyle</c>) as given. arialbd.ttf is "Arial" bold; arial.ttf is not.
        /// </summary>
        internal bool IsFace(string typeFace, bool bold, bool italic)
        {
            if (bold != ((_style & 1) != 0) || italic != ((_style & 2) != 0))
            {
                return false;
            }

            foreach (string family in _families)
            {
                if (string.Equals(family, typeFace, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the bytes of a file are of an OpenType font of CFF outlines, or a collection whose first face is one, whose ink is not read.</summary>
        internal static bool IsCff(byte[] bytes)
        {
            int at = Face(bytes);
            return at >= 0 && U32(bytes, at) == OfCff;
        }

        /// <summary>
        /// Reads a font from the bytes of its file; null where they are no TrueType font of glyf outlines, or a collection whose
        /// first face is none, one cut short, or one of no letter A that reaches above its baseline.
        /// </summary>
        internal static DxfTrueTypeFont Read(byte[] bytes)
        {
            int at = Face(bytes);

            if (at < 0 || (U32(bytes, at) != OfTrueType && U32(bytes, at) != OfTrue) || !Within(bytes, at, 12))
            {
                return null;
            }

            int count = U16(bytes, at + 4);

            if (!Within(bytes, at + 12, count * 16)
                || !Table(bytes, at, count, "head", 54, out int head, out _)
                || !Table(bytes, at, count, "hhea", 36, out int hhea, out _)
                || !Table(bytes, at, count, "maxp", 6, out int maxp, out _)
                || !Table(bytes, at, count, "hmtx", 4, out int hmtx, out int hmtxLength)
                || !Table(bytes, at, count, "cmap", 4, out int cmap, out int cmapLength)
                || !Table(bytes, at, count, "loca", 2, out int loca, out int locaLength)
                || !Table(bytes, at, count, "glyf", 0, out int glyf, out int glyfLength))
            {
                return null;
            }

            int form = U16(bytes, head + 50);
            int advances = U16(bytes, hhea + 34);
            int letters = U16(bytes, maxp + 4);

            if ((form != 0 && form != 1) || advances == 0 || advances * 4L > hmtxLength || letters == 0 || (letters + 1L) * (form == 1 ? 4 : 2) > locaLength)
            {
                return null;
            }

            var tables = new Tables
            {
                Metrics = hmtx,
                Advances = advances,
                Letters = letters,
                Loca = loca,
                LongLoca = form == 1,
                Glyf = glyf,
                GlyfEnd = glyf + glyfLength,
                MapEnd = cmap + cmapLength,
                Descent = Table(bytes, at, count, "OS/2", 78, out int os2, out _) ? U16(bytes, os2 + 76) : double.NaN,
                Families = Table(bytes, at, count, "name", 6, out int name, out int nameLength) ? Families(bytes, name, nameLength) : new string[0],
                Style = U16(bytes, head + 44),
                Collection = U32(bytes, 0) == OfCollection,
            };

            if (!Map(bytes, cmap, cmapLength, tables))
            {
                return null;
            }

            var font = new DxfTrueTypeFont(bytes, tables);
            int a = font.IndexOf('A');

            if (a <= 0 || !font.Header(a, out _, out _, out _, out int top) || top <= 0)
            {
                return null;
            }

            font._above = top;
            return font;
        }

        /// <summary>The letter of a Unicode number, its byte at U+F000 on in a font of symbols, read once; null where the font maps it to none.</summary>
        internal override Glyph GlyphOf(int number)
        {
            if (number <= 0)
            {
                return null;
            }

            lock (_glyphs)
            {
                if (!_glyphs.TryGetValue(number, out Glyph glyph))
                {
                    int index = IndexOf(number);
                    glyph = index > 0 ? Letter(index) : null;
                    _glyphs.Add(number, glyph);
                }

                return glyph;
            }
        }

        /// <summary>The index of the letter of a Unicode number, its byte at U+F000 on in a font of symbols; nought where the font maps it to none, or to a letter past its last.</summary>
        private int IndexOf(int number)
        {
            int index = Index(_symbol && number < 0x100 ? 0xF000 + number : number);
            return index < _letters ? index : 0;
        }

        /// <summary>
        /// The letter of an index as AutoCAD 2021 boxes it (check F2): its advance, and its ink as two corners of the box its
        /// glyf header gives, at least a unit wide and high as Windows boxes a letter (a space, of no outline, a unit square
        /// below the baseline at the pen), that box as high as it is from the lower of its bottom and the baseline, but up to
        /// the baseline at least: Arial's degree sign, inked from 936 to 1491 up, from 0 to 555, and its low line, inked from
        /// -407 to -277, from -407 to 0.
        /// </summary>
        private Glyph Letter(int index)
        {
            int advance = U16(_bytes, _metrics + (4 * (index < _advances ? index : _advances - 1)));

            if (!Header(index, out int xMin, out int yMin, out int xMax, out int yMax))
            {
                xMin = yMin = xMax = yMax = 0;
            }

            int wide = Math.Max(1, xMax - xMin);
            int high = Math.Max(1, yMax - yMin);
            double bottom = Math.Min(0, yMax - high);
            return new Glyph(advance, new double[] { xMin, xMin + wide }, new double[] { bottom, Math.Max(0.0, bottom + high) });
        }

        /// <summary>The box of the glyf header of the letter of an index; false where it has no outline, its header runs past the table, or its box is turned inside out.</summary>
        private bool Header(int index, out int xMin, out int yMin, out int xMax, out int yMax)
        {
            long from = _longLoca ? U32(_bytes, _loca + (4 * index)) : 2L * U16(_bytes, _loca + (2 * index));
            long to = _longLoca ? U32(_bytes, _loca + (4 * index) + 4) : 2L * U16(_bytes, _loca + (2 * index) + 2);
            xMin = yMin = xMax = yMax = 0;

            if (!(to > from && _glyf + from + 10 <= _glyfEnd))
            {
                return false;
            }

            int at = _glyf + (int)from;
            xMin = S16(_bytes, at + 2);
            yMin = S16(_bytes, at + 4);
            xMax = S16(_bytes, at + 6);
            yMax = S16(_bytes, at + 8);
            return xMin <= xMax && yMin <= yMax;
        }

        /// <summary>The index of the letter of a number in the subtable of <c>cmap</c> chosen; nought, the font's first, where it maps none.</summary>
        private int Index(int number) => _wide ? InGroups(number) : InSegments(number);

        /// <summary>The index of the letter of a number in a subtable of format 12, its groups of numbers searched by halves.</summary>
        private int InGroups(int number)
        {
            long low = 0;
            long high = U32(_bytes, _map + 12) - 1;

            while (low <= high)
            {
                long middle = (low + high) / 2;
                int at = _map + 16 + (int)(middle * 12);
                long start = U32(_bytes, at);
                long end = U32(_bytes, at + 4);

                if (number < start)
                {
                    high = middle - 1;
                }
                else if (number > end)
                {
                    low = middle + 1;
                }
                else
                {
                    long index = U32(_bytes, at + 8) + (number - start);
                    return index < int.MaxValue ? (int)index : 0;
                }
            }

            return 0;
        }

        /// <summary>The index of the letter of a number in a subtable of format 4, its segments searched by halves for the first ending at it or past it.</summary>
        private int InSegments(int number)
        {
            if (number > 0xFFFF)
            {
                return 0;
            }

            int twice = U16(_bytes, _map + 6);
            int segments = twice / 2;
            int ends = _map + 14;
            int first = 0, last = segments - 1, found = -1;

            while (first <= last)
            {
                int middle = (first + last) / 2;

                if (U16(_bytes, ends + (2 * middle)) >= number)
                {
                    found = middle;
                    last = middle - 1;
                }
                else
                {
                    first = middle + 1;
                }
            }

            if (found < 0)
            {
                return 0;
            }

            int starts = ends + twice + 2;
            int start = U16(_bytes, starts + (2 * found));

            if (start > number)
            {
                return 0;
            }

            int delta = U16(_bytes, starts + twice + (2 * found));
            int rangeAt = starts + (2 * twice) + (2 * found);
            int range = U16(_bytes, rangeAt);

            if (range == 0)
            {
                return (number + delta) & 0xFFFF;
            }

            long glyph = rangeAt + (long)range + (2L * (number - start));

            if (glyph + 2 > _mapEnd)
            {
                return 0;
            }

            int index0 = U16(_bytes, (int)glyph);
            return index0 == 0 ? 0 : (index0 + delta) & 0xFFFF;
        }

        /// <summary>The names of a face's family in its <c>name</c> table, those of the platform 3 (Windows), its strings of UTF-16 big-endian, each language's in the table's order.</summary>
        private static string[] Families(byte[] bytes, int name, int length)
        {
            var found = new List<string>();
            int count = U16(bytes, name + 2);
            long strings = name + (long)U16(bytes, name + 4);

            for (int i = 0; i < count && 6 + (12L * (i + 1)) <= length; i++)
            {
                int record = name + 6 + (12 * i);
                int size = U16(bytes, record + 8);
                long at = strings + U16(bytes, record + 10);

                if (U16(bytes, record) != 3 || U16(bytes, record + 6) != 1 || size % 2 != 0 || at + size > name + (long)length)
                {
                    continue;
                }

                var letters = new char[size / 2];

                for (int k = 0; k < letters.Length; k++)
                {
                    letters[k] = (char)U16(bytes, (int)at + (2 * k));
                }

                found.Add(new string(letters));
            }

            return found.ToArray();
        }

        /// <summary>
        /// Chooses the subtable of <c>cmap</c> a Windows font is read by, as Windows reads it: of the platform 3, encoding 10 and
        /// format 12, else encoding 1 and format 4, else the symbols', encoding 0 and format 4; false where it has none whole.
        /// </summary>
        private static bool Map(byte[] bytes, int cmap, int length, Tables tables)
        {
            int count = U16(bytes, cmap + 2);
            int best = 0;

            for (int i = 0; i < count && 4 + (8L * (i + 1)) <= length; i++)
            {
                int record = cmap + 4 + (8 * i);
                int platform = U16(bytes, record);
                int encoding = U16(bytes, record + 2);
                long offset = U32(bytes, record + 4);

                if (platform != 3 || offset + 16 > length)
                {
                    continue;
                }

                int at = cmap + (int)offset;
                int format = U16(bytes, at);
                int rank = encoding == 10 && format == 12 ? 3 : encoding == 1 && format == 4 ? 2 : encoding == 0 && format == 4 ? 1 : 0;

                if (rank <= best || !Whole(bytes, at, format, cmap + length))
                {
                    continue;
                }

                best = rank;
                tables.Map = at;
                tables.Wide = format == 12;
                tables.Symbol = rank == 1;
            }

            return best > 0;
        }

        /// <summary>Whether a subtable of <c>cmap</c> holds all its segments, or groups, within the table.</summary>
        private static bool Whole(byte[] bytes, int at, int format, int end)
        {
            if (format == 12)
            {
                return at + 16 + (12L * U32(bytes, at + 12)) <= end;
            }

            return at + 16 + (4L * U16(bytes, at + 6)) <= end;
        }

        /// <summary>Where the offset table of the font's face starts: at 0, or the first face's of a collection; -1 where the file is too short to say.</summary>
        private static int Face(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12)
            {
                return -1;
            }

            if (U32(bytes, 0) != OfCollection)
            {
                return 0;
            }

            long first = bytes.Length >= 16 && U32(bytes, 8) > 0 ? U32(bytes, 12) : -1;
            return first >= 0 && first + 12 <= bytes.Length ? (int)first : -1;
        }

        /// <summary>The place and length of a table of the face, false where it has none, or it runs past the file or is shorter than it has to be.</summary>
        private static bool Table(byte[] bytes, int face, int count, string tag, int least, out int at, out int length)
        {
            uint wanted = (uint)((tag[0] << 24) | (tag[1] << 16) | (tag[2] << 8) | tag[3]);

            for (int i = 0; i < count; i++)
            {
                int record = face + 12 + (16 * i);

                if (U32(bytes, record) == wanted)
                {
                    long offset = U32(bytes, record + 8);
                    long size = U32(bytes, record + 12);

                    if (size >= least && offset + size <= bytes.Length)
                    {
                        at = (int)offset;
                        length = (int)size;
                        return true;
                    }

                    break;
                }
            }

            at = 0;
            length = 0;
            return false;
        }

        private static bool Within(byte[] bytes, long at, long length) => at >= 0 && at + length <= bytes.Length;

        // Big-endian reads, nought past the end of the file, so that no read of a font cut short throws.
        private static int U16(byte[] bytes, int at) => at >= 0 && at + 2 <= bytes.Length ? (bytes[at] << 8) | bytes[at + 1] : 0;

        private static int S16(byte[] bytes, int at) => (short)U16(bytes, at);

        private static long U32(byte[] bytes, int at) => at >= 0 && at + 4 <= bytes.Length ? ((long)bytes[at] << 24) | ((long)bytes[at + 1] << 16) | ((long)bytes[at + 2] << 8) | bytes[at + 3] : 0;

        /// <summary>What the tables of a face give the font, gathered as they are read.</summary>
        private sealed class Tables
        {
            internal int Metrics { get; set; }

            internal int Advances { get; set; }

            internal int Letters { get; set; }

            internal int Loca { get; set; }

            internal bool LongLoca { get; set; }

            internal int Glyf { get; set; }

            internal int GlyfEnd { get; set; }

            internal int Map { get; set; }

            internal int MapEnd { get; set; }

            internal bool Wide { get; set; }

            internal bool Symbol { get; set; }

            internal double Descent { get; set; }

            internal string[] Families { get; set; }

            internal int Style { get; set; }

            internal bool Collection { get; set; }
        }
    }
}
