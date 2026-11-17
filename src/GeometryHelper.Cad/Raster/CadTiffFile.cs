using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// TIFF (.tif, .tiff), the first image of the file, in either byte order: stored plain, packed (PackBits), LZW or Deflate,
    /// or coded for fax (modified Huffman, Group 3, Group 4); in strips or in tiles; bilevel, 2, 4, 8 or 16 bits of grey or
    /// palette, RGB with or without alpha, and CMYK, chunky or planar, with the horizontal predictor. A TIFF coded as JPEG, a
    /// BigTIFF, floating point samples and orientations other than the top left are not read.
    /// </summary>
    internal static class CadTiffFile
    {
        /// <summary>Whether the bytes begin a TIFF: "II" and 42, or "MM" and 42.</summary>
        internal static bool LooksLikeTiff(byte[] d) =>
            d.Length >= 8
            && (
                (d[0] == 'I' && d[1] == 'I' && d[2] == 42 && d[3] == 0)
                || (d[0] == 'M' && d[1] == 'M' && d[2] == 0 && d[3] == 42)
            );

        /// <summary>Reads a TIFF: its size and resolution, and its pixels where asked.</summary>
        /// <param name="d">The bytes of the file.</param>
        /// <param name="pixels">Whether to decode the pixels.</param>
        /// <param name="raster">The picture; null where the bytes are no TIFF this reads.</param>
        internal static bool TryRead(byte[] d, bool pixels, out CadRaster raster)
        {
            raster = null;

            if (!LooksLikeTiff(d))
            {
                return false;
            }

            var tiff = new Reader(d);
            Dictionary<int, Entry> tags = tiff.Directory();

            if (tags == null)
            {
                return false;
            }

            int width = (int)tiff.One(tags, 256, 0);
            int height = (int)tiff.One(tags, 257, 0);
            long[] bitsOf = tiff.Many(tags, 258);
            int bits = bitsOf.Length > 0 ? (int)bitsOf[0] : 1;
            int compression = (int)tiff.One(tags, 259, 1);
            int photometric = (int)tiff.One(tags, 262, 1);
            int samples = (int)tiff.One(tags, 277, 1);
            int planar = (int)tiff.One(tags, 284, 1);
            int predictor = (int)tiff.One(tags, 317, 1);
            long format = tiff.One(tags, 339, 1);
            bool tiled = tags.ContainsKey(322) && tags.ContainsKey(323);
            bool supportedCompression =
                compression == 1
                || compression == 2
                || compression == 3
                || compression == 4
                || compression == 5
                || compression == 8
                || compression == 32946
                || compression == 32773;
            bool supportedPhotometric =
                ((photometric == 0 || photometric == 1 || photometric == 3) && samples >= 1)
                || (photometric == 2 && samples >= 3)
                || (photometric == 5 && samples >= 4);
            bool supportedBits = bits == 8 || bits == 16 || (samples == 1 && (bits == 1 || bits == 2 || bits == 4));

            foreach (long b in bitsOf)
            {
                supportedBits &= b == bits;
            }

            if (width <= 0 || height <= 0 || samples < 1 || samples > 8)
            {
                return false;
            }

            double[] xres = tiff.Rational(tags, 282);
            double[] yres = tiff.Rational(tags, 283);
            long unit = tiff.One(tags, 296, 2);
            double perUnit =
                unit == 2 ? 25.4
                : unit == 3 ? 10.0
                : 0.0;
            raster = new CadRaster
            {
                Width = width,
                Height = height,
                MillimetresX = perUnit > 0.0 && xres.Length > 0 && xres[0] > 0.0 ? perUnit / xres[0] : 0.0,
                MillimetresY = perUnit > 0.0 && yres.Length > 0 && yres[0] > 0.0 ? perUnit / yres[0] : 0.0,
                IsBitonal = samples == 1 && bits == 1,
            };

            // The size is known of any TIFF, so that AutoCAD's attach is not refused for what its pixels cannot be read of.
            if (!pixels)
            {
                return true;
            }

            if (
                !supportedCompression
                || !supportedPhotometric
                || !supportedBits
                || (planar != 1 && planar != 2)
                || (planar == 2 && bits < 8)
                || (format != 1 && format != 2 && format != 4)
                || (predictor != 1 && predictor != 2)
                || (predictor == 2 && bits < 8)
                || ((compression == 2 || compression == 3 || compression == 4) && (bits != 1 || samples != 1))
                || (tiled == false && !tags.ContainsKey(273))
                || (tiled && !tags.ContainsKey(324))
            )
            {
                raster = null;
                return false;
            }

            long orientation = tiff.One(tags, 274, 1);

            if (orientation != 1 || (double)width * height > CadRasterReader.MostRawPixels * 4.0)
            {
                raster = null;
                return false;
            }

            int tileWidth = tiled ? (int)tiff.One(tags, 322, 0) : width;
            long rows = tiled ? tiff.One(tags, 323, 0) : tiff.One(tags, 278, height);
            int tileLength = (int)Math.Min(rows <= 0 ? height : rows, height);

            if (tileWidth <= 0 || tileLength <= 0 || (tiled && (tileWidth > 1 << 20 || tileLength > 1 << 20)))
            {
                raster = null;
                return false;
            }

            int across = (width + tileWidth - 1) / tileWidth;
            int down = (height + tileLength - 1) / tileLength;
            int planes = planar == 2 ? samples : 1;
            int chunkSamples = planar == 2 ? 1 : samples;
            int chunkRowBytes = (int)(((long)tileWidth * chunkSamples * bits + 7) / 8);
            long[] offsets = tiff.Many(tags, tiled ? 324 : 273);
            long[] counts = tiff.Many(tags, tiled ? 325 : 279);
            long[] colourMap = tiff.Many(tags, 320);
            long[] extra = tiff.Many(tags, 338);
            int t4 = (int)tiff.One(tags, 292, 0);
            int fillOrder = (int)tiff.One(tags, 266, 1);
            int alphaKind = extra.Length > 0 ? (int)extra[0] : 0;
            var convert = new TiffPixels(photometric, samples, bits, tiff.LittleEndian, colourMap, alphaKind, width);
            var sink = new CadRasterSink(raster);
            var rowBuffer = new byte[(long)across * chunkRowBytes * (planar == 2 ? samples : 1)];
            var rgbaRow = new byte[width * 4];

            for (int band = 0; band < down; band++)
            {
                int bandRows = tiled ? tileLength : Math.Min(tileLength, height - band * tileLength);
                var chunks = new byte[planes][][];

                for (int p = 0; p < planes; p++)
                {
                    chunks[p] = new byte[across][];

                    for (int c = 0; c < across; c++)
                    {
                        int index = (p * down + band) * across + c;
                        byte[] raw = null;

                        if (index < offsets.Length)
                        {
                            long count = index < counts.Length ? counts[index] : d.Length - offsets[index];
                            raw = Unpack(
                                d,
                                offsets[index],
                                count,
                                compression,
                                bandRows,
                                chunkRowBytes,
                                tileWidth,
                                t4,
                                fillOrder,
                                photometric
                            );
                        }

                        if (raw == null)
                        {
                            raw = new byte[bandRows * chunkRowBytes];
                        }

                        if (predictor == 2)
                        {
                            Differencing(raw, bandRows, chunkRowBytes, chunkSamples, bits, tiff.LittleEndian);
                        }

                        chunks[p][c] = raw;
                    }
                }

                int rowsToUse = Math.Min(bandRows, height - band * tileLength);

                for (int r = 0; r < rowsToUse; r++)
                {
                    int bytesPerSample = bits / 8;

                    for (int c = 0; c < across; c++)
                    {
                        if (planar == 2)
                        {
                            for (int p = 0; p < planes; p++)
                            {
                                byte[] raw = chunks[p][c];

                                for (int x = 0; x < tileWidth; x++)
                                {
                                    int from = r * chunkRowBytes + x * bytesPerSample;
                                    int to = ((c * tileWidth + x) * samples + p) * bytesPerSample;

                                    for (int k = 0; k < bytesPerSample; k++)
                                    {
                                        rowBuffer[to + k] = from + k < raw.Length ? raw[from + k] : (byte)0;
                                    }
                                }
                            }
                        }
                        else
                        {
                            int available = Math.Min(
                                chunkRowBytes,
                                Math.Max(0, chunks[0][c].Length - r * chunkRowBytes)
                            );
                            Buffer.BlockCopy(chunks[0][c], r * chunkRowBytes, rowBuffer, c * chunkRowBytes, available);
                        }
                    }

                    convert.Row(rowBuffer, rgbaRow);
                    sink.Add(rgbaRow, 0);
                }
            }

            raster = sink.Finish();
            return true;
        }

        /// <summary>The rows of a strip or a tile as the samples lie in them, unpacked from the coding the file says.</summary>
        private static byte[] Unpack(
            byte[] d,
            long offset,
            long count,
            int compression,
            int rows,
            int rowBytes,
            int width,
            int t4,
            int fillOrder,
            int photometric
        )
        {
            if (offset < 0 || offset >= d.Length || count <= 0)
            {
                return null;
            }

            int start = (int)offset;
            int length = (int)Math.Min(count, d.Length - offset);
            int expected = rows * rowBytes;

            try
            {
                switch (compression)
                {
                    case 1:
                    {
                        var raw = new byte[expected];
                        Buffer.BlockCopy(d, start, raw, 0, Math.Min(expected, length));
                        return raw;
                    }
                    case 5:
                        return Lzw(d, start, length, expected);
                    case 8:
                    case 32946:
                        return Deflate(d, start, length, expected);
                    case 32773:
                        return PackBits(d, start, length, expected);
                    default:
                    {
                        byte[] source = d;
                        int at = start;

                        if (fillOrder == 2)
                        {
                            source = new byte[length];

                            for (int i = 0; i < length; i++)
                            {
                                source[i] = Reverse(d[start + i]);
                            }

                            at = 0;
                        }

                        byte[] raw = CadCcitt.Decode(source, at, length, width, rows, compression, (t4 & 1) != 0);

                        if (photometric == 1)
                        {
                            // Black is 1 as the decoder gives it; BlackIsZero wants white to be 1.
                            for (int i = 0; i < raw.Length; i++)
                            {
                                raw[i] = (byte)~raw[i];
                            }
                        }

                        return raw;
                    }
                }
            }
            catch (Exception exception)
                when (exception is InvalidDataException
                    || exception is IndexOutOfRangeException
                    || exception is ArgumentException
                    || exception is IOException
                )
            {
                // A strip that cannot be unpacked is left blank; the others are still shown.
                return null;
            }
        }

        private static byte Reverse(byte b)
        {
            int v = b;
            v = ((v & 0xF0) >> 4) | ((v & 0x0F) << 4);
            v = ((v & 0xCC) >> 2) | ((v & 0x33) << 2);
            v = ((v & 0xAA) >> 1) | ((v & 0x55) << 1);
            return (byte)v;
        }

        /// <summary>
        /// LZW as a TIFF holds it: codes of 9 to 12 bits, the most significant bit first, 256 clearing the table and 257 ending,
        /// the code width grown one code early.
        /// </summary>
        internal static byte[] Lzw(byte[] d, int start, int length, int expected)
        {
            var output = new byte[expected + 4096];
            var prefix = new int[4096];
            var first = new byte[4096];
            var suffix = new byte[4096];
            var size = new int[4096];

            for (int i = 0; i < 256; i++)
            {
                prefix[i] = -1;
                first[i] = (byte)i;
                suffix[i] = (byte)i;
                size[i] = 1;
            }

            int next = 258;
            int codeBits = 9;
            int previous = -1;
            int position = 0;
            long bit = (long)start * 8;
            long end = ((long)start + length) * 8;

            while (position < expected && bit + codeBits <= end)
            {
                int code = 0;

                for (int i = 0; i < codeBits; i++)
                {
                    long p = bit + i;
                    code = (code << 1) | ((d[p >> 3] >> (7 - (int)(p & 7))) & 1);
                }

                bit += codeBits;

                if (code == 257)
                {
                    break;
                }

                if (code == 256)
                {
                    codeBits = 9;
                    next = 258;
                    previous = -1;
                    continue;
                }

                if (previous < 0)
                {
                    if (code > 255)
                    {
                        break;
                    }

                    output[position++] = (byte)code;
                    previous = code;
                    continue;
                }

                if (code > next || (code == next && next >= 4096))
                {
                    break;
                }

                if (next < 4096)
                {
                    // The new string is the one before it and the first byte of this code's string (of the one before
                    // it where this code is the very string being made).
                    prefix[next] = previous;
                    suffix[next] = code < next ? first[code] : first[previous];
                    first[next] = first[previous];
                    size[next] = size[previous] + 1;
                    next++;
                }

                int length2 = size[code];
                int at = position + length2;

                for (int c = code; c >= 0 && at > position; c = prefix[c])
                {
                    output[--at] = suffix[c];
                }

                position += length2;
                previous = code;

                if (next == 511)
                {
                    codeBits = 10;
                }
                else if (next == 1023)
                {
                    codeBits = 11;
                }
                else if (next == 2047)
                {
                    codeBits = 12;
                }
            }

            var result = new byte[expected];
            Buffer.BlockCopy(output, 0, result, 0, Math.Min(expected, position));
            return result;
        }

        private static byte[] Deflate(byte[] d, int start, int length, int expected)
        {
            var raw = new byte[expected];

            // The zlib header of two bytes comes before the deflate data; its check sum after it is not read.
            using (var stream = new MemoryStream(d, start + 2, Math.Max(0, length - 2)))
            using (var inflate = new DeflateStream(stream, CompressionMode.Decompress))
            {
                int got = 0;

                while (got < expected)
                {
                    int n = inflate.Read(raw, got, expected - got);

                    if (n <= 0)
                    {
                        break;
                    }

                    got += n;
                }
            }

            return raw;
        }

        internal static byte[] PackBits(byte[] d, int start, int length, int expected)
        {
            var raw = new byte[expected];
            int at = start;
            int end = start + length;
            int to = 0;

            while (at < end && to < expected)
            {
                int n = (sbyte)d[at++];

                if (n >= 0)
                {
                    int count = n + 1;

                    for (int i = 0; i < count && at < end && to < expected; i++)
                    {
                        raw[to++] = d[at++];
                    }
                }
                else if (n != -128)
                {
                    int count = 1 - n;

                    if (at >= end)
                    {
                        break;
                    }

                    byte value = d[at++];

                    for (int i = 0; i < count && to < expected; i++)
                    {
                        raw[to++] = value;
                    }
                }
            }

            return raw;
        }

        /// <summary>Undoes the horizontal predictor: each sample was stored as its difference from the one before it in the row.</summary>
        private static void Differencing(byte[] raw, int rows, int rowBytes, int samples, int bits, bool little)
        {
            for (int r = 0; r < rows; r++)
            {
                int row = r * rowBytes;

                if (bits == 8)
                {
                    for (int i = samples; i < rowBytes && row + i < raw.Length; i++)
                    {
                        raw[row + i] = (byte)(raw[row + i] + raw[row + i - samples]);
                    }
                }
                else
                {
                    int step = 2 * samples;

                    for (int i = step; i + 1 < rowBytes && row + i + 1 < raw.Length; i += 2)
                    {
                        int now = little
                            ? raw[row + i] | (raw[row + i + 1] << 8)
                            : (raw[row + i] << 8) | raw[row + i + 1];
                        int before = little
                            ? raw[row + i - step] | (raw[row + i - step + 1] << 8)
                            : (raw[row + i - step] << 8) | raw[row + i - step + 1];
                        int sum = (now + before) & 0xFFFF;

                        if (little)
                        {
                            raw[row + i] = (byte)sum;
                            raw[row + i + 1] = (byte)(sum >> 8);
                        }
                        else
                        {
                            raw[row + i] = (byte)(sum >> 8);
                            raw[row + i + 1] = (byte)sum;
                        }
                    }
                }
            }
        }

        private sealed class Entry
        {
            internal int Type;
            internal long Count;
            internal long At;
        }

        /// <summary>The directory of a TIFF and its values, read in the file's byte order.</summary>
        private sealed class Reader
        {
            private readonly byte[] _d;

            internal Reader(byte[] d)
            {
                _d = d;
                LittleEndian = d[0] == 'I';
            }

            internal bool LittleEndian { get; }

            internal int U16(long at) =>
                at < 0 || at + 2 > _d.Length ? 0
                : LittleEndian ? _d[at] | (_d[at + 1] << 8)
                : (_d[at] << 8) | _d[at + 1];

            internal long U32(long at) =>
                at < 0 || at + 4 > _d.Length ? 0
                : LittleEndian
                    ? (long)_d[at] | ((long)_d[at + 1] << 8) | ((long)_d[at + 2] << 16) | ((long)_d[at + 3] << 24)
                : ((long)_d[at] << 24) | ((long)_d[at + 1] << 16) | ((long)_d[at + 2] << 8) | _d[at + 3];

            internal Dictionary<int, Entry> Directory()
            {
                long ifd = U32(4);

                if (ifd < 8 || ifd + 2 > _d.Length)
                {
                    return null;
                }

                int n = U16(ifd);
                var tags = new Dictionary<int, Entry>();

                for (int i = 0; i < n; i++)
                {
                    long at = ifd + 2 + 12L * i;

                    if (at + 12 > _d.Length)
                    {
                        break;
                    }

                    var entry = new Entry { Type = U16(at + 2), Count = U32(at + 4) };
                    long size = Size(entry.Type) * entry.Count;
                    entry.At = size <= 4 ? at + 8 : U32(at + 8);
                    tags[U16(at)] = entry;
                }

                return tags;
            }

            private static int Size(int type)
            {
                switch (type)
                {
                    case 1:
                    case 2:
                    case 6:
                    case 7:
                        return 1;
                    case 3:
                    case 8:
                        return 2;
                    case 4:
                    case 9:
                    case 11:
                        return 4;
                    case 5:
                    case 10:
                    case 12:
                        return 8;
                    default:
                        return 0;
                }
            }

            /// <summary>The whole numbers a tag holds (bytes, shorts and longs); none for a tag the file lacks.</summary>
            internal long[] Many(Dictionary<int, Entry> tags, int tag)
            {
                if (!tags.TryGetValue(tag, out Entry e) || e.Count <= 0 || e.Count > 1 << 26)
                {
                    return new long[0];
                }

                int size = Size(e.Type);

                if (
                    size == 0
                    || e.Type == 5
                    || e.Type == 10
                    || e.Type == 11
                    || e.Type == 12
                    || e.At + size * e.Count > _d.Length
                )
                {
                    return new long[0];
                }

                var values = new long[e.Count];

                for (long i = 0; i < e.Count; i++)
                {
                    long at = e.At + i * size;
                    values[i] =
                        size == 1 ? _d[at]
                        : size == 2 ? U16(at)
                        : U32(at);
                }

                return values;
            }

            internal long One(Dictionary<int, Entry> tags, int tag, long fallback)
            {
                long[] values = Many(tags, tag);
                return values.Length > 0 ? values[0] : fallback;
            }

            internal double[] Rational(Dictionary<int, Entry> tags, int tag)
            {
                if (!tags.TryGetValue(tag, out Entry e) || e.Type != 5 || e.Count < 1 || e.At + 8 > _d.Length)
                {
                    return new double[0];
                }

                long denominator = U32(e.At + 4);
                return denominator == 0 ? new double[0] : new[] { (double)U32(e.At) / denominator };
            }
        }

        /// <summary>Turns a row of samples into pixels of 8-bit red, green, blue and alpha.</summary>
        private sealed class TiffPixels
        {
            private readonly int _photometric;
            private readonly int _samples;
            private readonly int _bits;
            private readonly bool _little;
            private readonly long[] _map;
            private readonly int _alpha;
            private readonly int _width;

            internal TiffPixels(int photometric, int samples, int bits, bool little, long[] map, int alpha, int width)
            {
                _photometric = photometric;
                _samples = samples;
                _bits = bits;
                _little = little;
                _map = map;
                _alpha = alpha;
                _width = width;
            }

            private int Sample(byte[] row, int x, int s)
            {
                int at = x * _samples + s;

                if (_bits == 8)
                {
                    return row[at];
                }

                return row[at * 2 + (_little ? 1 : 0)];
            }

            internal void Row(byte[] row, byte[] rgba)
            {
                int levels = _bits <= 8 ? (1 << _bits) - 1 : 255;

                for (int x = 0; x < _width; x++)
                {
                    int r;
                    int g;
                    int b;
                    int a = 255;

                    if (_photometric == 2)
                    {
                        r = Sample(row, x, 0);
                        g = Sample(row, x, 1);
                        b = Sample(row, x, 2);

                        if (_samples > 3 && (_alpha == 1 || _alpha == 2 || _alpha == 0))
                        {
                            a = Sample(row, x, 3);

                            if (_alpha == 1 && a > 0 && a < 255)
                            {
                                r = Math.Min(255, r * 255 / a);
                                g = Math.Min(255, g * 255 / a);
                                b = Math.Min(255, b * 255 / a);
                            }
                        }
                    }
                    else if (_photometric == 5)
                    {
                        int c = Sample(row, x, 0);
                        int m = Sample(row, x, 1);
                        int y = Sample(row, x, 2);
                        int k = Sample(row, x, 3);
                        r = (255 - c) * (255 - k) / 255;
                        g = (255 - m) * (255 - k) / 255;
                        b = (255 - y) * (255 - k) / 255;
                    }
                    else
                    {
                        int v;

                        if (_bits < 8)
                        {
                            int bit = x * _bits;
                            v = (row[bit >> 3] >> (8 - _bits - (bit & 7))) & levels;
                        }
                        else
                        {
                            v = Sample(row, x, 0);
                        }

                        if (_photometric == 3)
                        {
                            int n = 1 << _bits;

                            if (_bits == 16)
                            {
                                v = 0;
                            }

                            r = n * 3 <= _map.Length ? (int)(_map[v] >> 8) : v;
                            g = n * 3 <= _map.Length ? (int)(_map[n + v] >> 8) : v;
                            b = n * 3 <= _map.Length ? (int)(_map[2 * n + v] >> 8) : v;
                        }
                        else
                        {
                            int level = _bits < 8 ? v * 255 / levels : v;

                            if (_photometric == 0)
                            {
                                level = 255 - level;
                            }

                            r = level;
                            g = level;
                            b = level;

                            if (_samples == 2 && _alpha != 0)
                            {
                                a = Sample(row, x, 1);
                            }
                        }
                    }

                    rgba[4 * x] = (byte)r;
                    rgba[4 * x + 1] = (byte)g;
                    rgba[4 * x + 2] = (byte)b;
                    rgba[4 * x + 3] = (byte)a;
                }
            }
        }
    }
}
