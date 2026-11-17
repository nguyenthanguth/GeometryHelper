using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What WMFIN takes from a TrueType font file to place a text as AutoCAD does: the ascent of the font's cell, the height of
    /// its capitals, the depth under the baseline, and the advance of each letter, all as parts of the em. Read once per file
    /// from the head, hhea, OS/2, hmtx and cmap tables (the Windows Unicode map of format 4); a file that cannot be read gives none.
    /// </summary>
    internal sealed class CadFontMetrics
    {
        private static readonly Dictionary<string, CadFontMetrics> Cache = new Dictionary<string, CadFontMetrics>(
            StringComparer.OrdinalIgnoreCase
        );

        private readonly int _units;
        private readonly int[] _advances;
        private readonly int[] _starts;
        private readonly int[] _ends;
        private readonly int[] _deltas;
        private readonly int[] _offsets;
        private readonly byte[] _glyphs;

        private CadFontMetrics(
            int units,
            int[] advances,
            int[] starts,
            int[] ends,
            int[] deltas,
            int[] offsets,
            byte[] glyphs
        )
        {
            _units = units;
            _advances = advances;
            _starts = starts;
            _ends = ends;
            _deltas = deltas;
            _offsets = offsets;
            _glyphs = glyphs;
        }

        /// <summary>The ascent of the font's cell (the Windows ascent) as a part of the em: 0.905 for Arial.</summary>
        public double Ascent { get; private set; }

        /// <summary>The depth of the font's cell under the baseline (the Windows descent) as a part of the em: 0.212 for Arial.</summary>
        public double Descent { get; private set; }

        /// <summary>The height of the capitals as a part of the em, 0 where the font does not give it: 0.716 for Arial.</summary>
        public double CapHeight { get; private set; }

        /// <summary>
        /// Gets the metrics of a font file, null where the file is none, or is no TrueType font (kept, as the file is no font). A
        /// file that cannot be read at all (an <see cref="IOException"/>, as one held open by another program gives, or a refused
        /// access) gives null too, and is read again by the next call.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The metrics, or null.</returns>
        public static CadFontMetrics Of(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            lock (Cache)
            {
                if (!Cache.TryGetValue(path, out CadFontMetrics metrics))
                {
                    metrics = Read(path, out bool readFailed);

                    if (!readFailed)
                    {
                        Cache[path] = metrics;
                    }
                }

                return metrics;
            }
        }

        /// <summary>Gets the width of a text in the font, the advances of its letters added, as a part of the em; 0.5 an em for a letter the font has no glyph of.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The width.</returns>
        public double Width(string text)
        {
            double width = 0.0;

            foreach (char letter in text ?? string.Empty)
            {
                width += Advance(Glyph(letter)) / (double)_units;
            }

            return width;
        }

        private int Advance(int glyph) =>
            _advances.Length == 0 ? _units / 2 : _advances[Math.Min(glyph, _advances.Length - 1)];

        private int Glyph(int code)
        {
            for (int i = 0; i < _ends.Length; i++)
            {
                if (code > _ends[i])
                {
                    continue;
                }

                if (code < _starts[i])
                {
                    return 0;
                }

                if (_offsets[i] == 0)
                {
                    return (code + _deltas[i]) & 0xFFFF;
                }

                int at = _offsets[i] - (_ends.Length - i) * 2 + (code - _starts[i]) * 2;

                if (at < 0 || at + 1 >= _glyphs.Length)
                {
                    return 0;
                }

                int glyph = (_glyphs[at] << 8) | _glyphs[at + 1];
                return glyph == 0 ? 0 : (glyph + _deltas[i]) & 0xFFFF;
            }

            return 0;
        }

        private static CadFontMetrics Read(string path, out bool readFailed)
        {
            readFailed = false;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    long font = 0;

                    if (Tag(reader) == "ttcf")
                    {
                        stream.Position = 12;
                        font = Big32(reader);
                    }

                    stream.Position = font + 4;
                    int tables = Big16(reader);
                    var offsets = new Dictionary<string, long[]>();

                    for (int i = 0; i < tables; i++)
                    {
                        stream.Position = font + 12 + 16 * i;
                        string tag = Tag(reader);
                        Big32(reader);
                        offsets[tag] = new[] { Big32(reader), Big32(reader) };
                    }

                    if (
                        !offsets.TryGetValue("head", out long[] head)
                        || !offsets.TryGetValue("hhea", out long[] hhea)
                        || !offsets.TryGetValue("OS/2", out long[] os2)
                    )
                    {
                        return null;
                    }

                    stream.Position = head[0] + 18;
                    int units = Big16(reader);

                    if (units <= 0)
                    {
                        return null;
                    }

                    stream.Position = hhea[0] + 34;
                    int hMetrics = Big16(reader);
                    stream.Position = os2[0];
                    int version = Big16(reader);
                    stream.Position = os2[0] + 74;
                    int winAscent = Big16(reader);
                    int winDescent = Big16(reader);
                    int cap = 0;

                    if (version >= 2 && os2[1] >= 90)
                    {
                        stream.Position = os2[0] + 88;
                        cap = Big16(reader);
                    }

                    var advances = new int[0];

                    if (offsets.TryGetValue("hmtx", out long[] hmtx))
                    {
                        advances = new int[hMetrics];
                        stream.Position = hmtx[0];

                        for (int i = 0; i < hMetrics; i++)
                        {
                            advances[i] = Big16(reader);
                            Big16(reader);
                        }
                    }

                    int[] starts = new int[0];
                    int[] ends = starts;
                    int[] deltas = starts;
                    int[] rangeOffsets = starts;
                    byte[] glyphs = new byte[0];

                    if (offsets.TryGetValue("cmap", out long[] cmap))
                    {
                        stream.Position = cmap[0] + 2;
                        int count = Big16(reader);
                        long subtable = -1;

                        for (int i = 0; i < count; i++)
                        {
                            stream.Position = cmap[0] + 4 + 8 * i;
                            int platform = Big16(reader);
                            int encoding = Big16(reader);
                            long offset = Big32(reader);

                            if (platform == 3 && (encoding == 1 || subtable < 0))
                            {
                                subtable = cmap[0] + offset;
                            }
                        }

                        if (subtable >= 0)
                        {
                            stream.Position = subtable;

                            if (Big16(reader) == 4)
                            {
                                int length = Big16(reader);
                                Big16(reader);
                                int segments = Big16(reader) / 2;
                                stream.Position = subtable + 14;
                                ends = ReadWords(reader, segments);
                                Big16(reader);
                                starts = ReadWords(reader, segments);
                                deltas = ReadWords(reader, segments);
                                rangeOffsets = ReadWords(reader, segments);
                                long rest = subtable + length - stream.Position;
                                glyphs = rest > 0 ? reader.ReadBytes((int)rest) : new byte[0];
                            }
                        }
                    }

                    return new CadFontMetrics(units, advances, starts, ends, deltas, rangeOffsets, glyphs)
                    {
                        Ascent = winAscent / (double)units,
                        Descent = winDescent / (double)units,
                        CapHeight = cap / (double)units,
                    };
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                readFailed = true;
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static int[] ReadWords(BinaryReader reader, int count)
        {
            var words = new int[count];

            for (int i = 0; i < count; i++)
            {
                words[i] = Big16(reader);
            }

            return words;
        }

        private static string Tag(BinaryReader reader) => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));

        private static int Big16(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(2);
            return b.Length < 2 ? 0 : (b[0] << 8) | b[1];
        }

        private static long Big32(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(4);
            return b.Length < 4 ? 0 : ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
        }
    }
}
