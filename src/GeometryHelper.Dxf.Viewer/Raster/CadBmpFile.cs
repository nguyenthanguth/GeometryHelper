using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Windows bitmaps: a BMP with its file header, a DIB (the same without that header, .dib) and an RLE (a bitmap of 4 or 8
    /// bits run-length coded, .rle), of 1, 2, 4, 8, 16, 24 or 32 bits a pixel, with a palette, with bit masks, from the top or
    /// from the bottom. Where the platform decodes a BMP itself the presenter leaves it to that; this reads the ones it does
    /// not (a DIB, an RLE, an odd mask) and gives the size and resolution of any.
    /// </summary>
    internal static class CadBmpFile
    {
        /// <summary>Whether the bytes begin a bitmap with no file header: the size of an info header, then a plausible width, height and depth.</summary>
        internal static bool LooksLikeDib(byte[] d)
        {
            if (d.Length < 16)
            {
                return false;
            }

            int size = Le32(d, 0);

            if (size == 12)
            {
                return Le16(d, 4) > 0 && Le16(d, 6) > 0 && Le16(d, 8) == 1 && IsDepth(Le16(d, 10));
            }

            if ((size != 40 && size != 52 && size != 56 && size != 64 && size != 108 && size != 124) || d.Length < 40)
            {
                return false;
            }

            int h = Le32(d, 8);
            return Le32(d, 4) > 0
                && h != 0
                && Math.Abs((long)h) < int.MaxValue
                && Le16(d, 12) == 1
                && IsDepth(Le16(d, 14));
        }

        /// <summary>
        /// Reads a bitmap: its size and resolution, and its pixels where asked. A file header ("BM") is read where there is one.
        /// </summary>
        /// <param name="d">The bytes of the file.</param>
        /// <param name="pixels">Whether to decode the pixels.</param>
        /// <param name="raster">The picture; null where the bytes are no bitmap this reads.</param>
        internal static bool TryRead(byte[] d, bool pixels, out CadRaster raster)
        {
            raster = null;
            int at = 0;
            int bitsOffset = -1;

            if (d.Length >= 14 && d[0] == 'B' && d[1] == 'M')
            {
                bitsOffset = Le32(d, 10);
                at = 14;
            }

            if (d.Length < at + 12)
            {
                return false;
            }

            int size = Le32(d, at);
            int width;
            int height;
            int bits;
            int compression = 0;
            int used = 0;
            double mx = 0.0;
            double my = 0.0;
            int entry = 4;

            if (size == 12)
            {
                width = Le16(d, at + 4);
                height = Le16(d, at + 6);
                bits = Le16(d, at + 10);
                entry = 3;
            }
            else if (size >= 40 && d.Length >= at + 40)
            {
                width = Le32(d, at + 4);
                height = Le32(d, at + 8);
                bits = Le16(d, at + 14);
                compression = Le32(d, at + 16);
                int x = Le32(d, at + 24);
                int y = Le32(d, at + 28);
                used = Le32(d, at + 32);
                mx = x > 0 ? 1000.0 / x : 0.0;
                my = y > 0 ? 1000.0 / y : 0.0;
            }
            else
            {
                return false;
            }

            bool topDown = height < 0;
            height = Math.Abs(height);

            if (width <= 0 || height <= 0 || !IsDepth(bits) || compression < 0 || compression > 6)
            {
                return false;
            }

            raster = new CadRaster
            {
                Width = width,
                Height = height,
                MillimetresX = mx,
                MillimetresY = my,
                IsBitonal = bits == 1,
            };

            if (!pixels)
            {
                return true;
            }

            if (
                (compression == 1 && bits != 8)
                || (compression == 2 && bits != 4)
                || ((compression == 3 || compression == 6) && bits != 16 && bits != 32)
                || compression == 4
                || compression == 5
                || (double)width * height > CadRasterReader.MostRawPixels
            )
            {
                raster = null;
                return false;
            }

            // The masks of a bitmap of bit fields: after a header of 40 bytes, or inside a longer one.
            uint[] masks = null;
            int palette = at + size;

            if (compression == 3 || compression == 6)
            {
                int count = compression == 6 ? 4 : 3;
                int from = at + 40;
                masks = new uint[4];

                if (d.Length < from + 4 * count)
                {
                    raster = null;
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    masks[i] = (uint)Le32(d, from + 4 * i);
                }

                if (size == 40)
                {
                    palette += 4 * count;
                }
            }
            else if (bits == 16 || bits == 32)
            {
                masks =
                    bits == 16
                        ? new uint[] { 0x7C00, 0x03E0, 0x001F, 0 }
                        : new uint[] { 0x00FF0000, 0x0000FF00, 0x000000FF, 0 };
            }

            int[] colours = null;

            if (bits <= 8)
            {
                int count = used > 0 && used <= 1 << bits ? used : 1 << bits;
                colours = new int[1 << bits];

                for (int i = 0; i < colours.Length; i++)
                {
                    int p = palette + i * entry;

                    // A colour the palette does not hold is black.
                    colours[i] = i < count && p + 2 < d.Length ? (d[p + 2] << 16) | (d[p + 1] << 8) | d[p] : 0;
                }

                palette += count * entry;
            }

            if (bitsOffset < 0 || bitsOffset < palette || bitsOffset > d.Length)
            {
                bitsOffset =
                    bitsOffset >= 0 && bitsOffset >= at + size && bitsOffset <= d.Length ? bitsOffset : palette;
            }

            if (bitsOffset > d.Length)
            {
                raster = null;
                return false;
            }

            var sink = new CadRasterSink(raster);
            var row = new byte[width * 4];

            if (compression == 1 || compression == 2)
            {
                byte[] index = Rle(d, bitsOffset, width, height, compression == 2);

                for (int y = 0; y < height; y++)
                {
                    // The rows of a run-length bitmap are numbered from the bottom, whatever its height says.
                    int source = topDown ? y : height - 1 - y;

                    for (int x = 0; x < width; x++)
                    {
                        int c = colours[index[source * width + x]];
                        Put(row, x, c);
                    }

                    sink.Add(row, 0);
                }

                raster = sink.Finish();
                return true;
            }

            int stride = (int)(((long)width * bits + 31) / 32 * 4);

            for (int y = 0; y < height; y++)
            {
                int source = topDown ? y : height - 1 - y;
                long start = (long)bitsOffset + (long)source * stride;

                if (start + (((long)width * bits + 7) / 8) > d.Length)
                {
                    // A file cut short: the rows it lacks are transparent.
                    Array.Clear(row, 0, row.Length);
                    sink.Add(row, 0);
                    continue;
                }

                int s = (int)start;

                switch (bits)
                {
                    case 1:
                    case 2:
                    case 4:
                    case 8:
                        for (int x = 0; x < width; x++)
                        {
                            int bit = x * bits;
                            int value = (d[s + (bit >> 3)] >> (8 - bits - (bit & 7))) & ((1 << bits) - 1);
                            Put(row, x, colours[value]);
                        }

                        break;
                    case 24:
                        for (int x = 0; x < width; x++)
                        {
                            Put(row, x, (d[s + 3 * x + 2] << 16) | (d[s + 3 * x + 1] << 8) | d[s + 3 * x]);
                        }

                        break;
                    default:
                        for (int x = 0; x < width; x++)
                        {
                            uint v = bits == 16 ? (uint)Le16(d, s + 2 * x) : (uint)Le32(d, s + 4 * x);
                            int r = Scale(v, masks[0]);
                            int g = Scale(v, masks[1]);
                            int b = Scale(v, masks[2]);
                            int a = masks[3] == 0 ? 255 : Scale(v, masks[3]);
                            row[4 * x] = (byte)r;
                            row[4 * x + 1] = (byte)g;
                            row[4 * x + 2] = (byte)b;
                            row[4 * x + 3] = (byte)a;
                        }

                        break;
                }

                sink.Add(row, 0);
            }

            raster = sink.Finish();
            return true;
        }

        private static void Put(byte[] row, int x, int rgb)
        {
            row[4 * x] = (byte)(rgb >> 16);
            row[4 * x + 1] = (byte)(rgb >> 8);
            row[4 * x + 2] = (byte)rgb;
            row[4 * x + 3] = 255;
        }

        /// <summary>The value a mask picks from a pixel, stretched to 0 to 255: the 5 bits of a 555 colour are 0 to 31 and give 0 to 255.</summary>
        private static int Scale(uint value, uint mask)
        {
            if (mask == 0)
            {
                return 0;
            }

            int shift = 0;

            while (((mask >> shift) & 1) == 0)
            {
                shift++;
            }

            uint top = mask >> shift;
            uint part = (value & mask) >> shift;
            return (int)((part * 255 + top / 2) / top);
        }

        /// <summary>
        /// Unpacks the run-length coding of a bitmap of 8 bits (RLE8) or 4 (RLE4) into one palette index for each pixel, the rows
        /// from the bottom. An absolute run, a run, the end of a line, the end of the bitmap and a jump are all read; what is
        /// not drawn is index 0.
        /// </summary>
        private static byte[] Rle(byte[] d, int from, int width, int height, bool four)
        {
            var index = new byte[width * height];
            int x = 0;
            int y = 0;
            int at = from;

            while (at + 1 < d.Length && y < height)
            {
                int count = d[at++];
                int value = d[at++];

                if (count > 0)
                {
                    for (int i = 0; i < count && x < width; i++)
                    {
                        index[y * width + x++] = four ? (byte)((i & 1) == 0 ? value >> 4 : value & 15) : (byte)value;
                    }

                    continue;
                }

                if (value == 0)
                {
                    x = 0;
                    y++;
                }
                else if (value == 1)
                {
                    break;
                }
                else if (value == 2)
                {
                    if (at + 1 >= d.Length)
                    {
                        break;
                    }

                    x += d[at++];
                    y += d[at++];
                }
                else
                {
                    int bytes = four ? (value + 1) / 2 : value;

                    for (int i = 0; i < value && at + (four ? i / 2 : i) < d.Length; i++)
                    {
                        int b = d[at + (four ? i / 2 : i)];

                        if (x < width && y < height)
                        {
                            index[y * width + x] = four ? (byte)((i & 1) == 0 ? b >> 4 : b & 15) : (byte)b;
                        }

                        x++;
                    }

                    at += bytes + (bytes & 1);
                }
            }

            return index;
        }

        private static bool IsDepth(int bits) =>
            bits == 1 || bits == 2 || bits == 4 || bits == 8 || bits == 16 || bits == 24 || bits == 32;

        private static int Le16(byte[] d, int at) => d[at] | (d[at + 1] << 8);

        private static int Le32(byte[] d, int at) => d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24);
    }
}
