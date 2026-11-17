using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Truevision TGA (.tga): colour-mapped (type 1), true colour (2) and grey (3), plain or run-length coded (types 9, 10 and
    /// 11), of 8, 15, 16, 24 or 32 bits a pixel, from the top or the bottom, from the left or the right. A TGA has no
    /// signature, so a file is taken for one by its name or by a header that makes sense; it gives no resolution.
    /// </summary>
    internal static class CadTgaFile
    {
        /// <summary>Whether the 18 bytes of a TGA header describe a picture this reads.</summary>
        internal static bool LooksLikeTga(byte[] d)
        {
            if (d.Length < 18)
            {
                return false;
            }

            int mapType = d[1];
            int type = d[2];
            int bits = d[16];
            int width = d[12] | (d[13] << 8);
            int height = d[14] | (d[15] << 8);

            if (width <= 0 || height <= 0 || mapType > 1)
            {
                return false;
            }

            switch (type)
            {
                case 1:
                case 9:
                    return mapType == 1 && bits == 8 && IsMapDepth(d[7]);
                case 2:
                case 10:
                    return bits == 15 || bits == 16 || bits == 24 || bits == 32;
                case 3:
                case 11:
                    return bits == 8;
                default:
                    return false;
            }
        }

        /// <summary>Reads a TGA: its size, and its pixels where asked.</summary>
        /// <param name="d">The bytes of the file.</param>
        /// <param name="pixels">Whether to decode the pixels.</param>
        /// <param name="raster">The picture; null where the bytes are no TGA this reads.</param>
        internal static bool TryRead(byte[] d, bool pixels, out CadRaster raster)
        {
            raster = null;

            if (!LooksLikeTga(d))
            {
                return false;
            }

            int idLength = d[0];
            int type = d[2];
            int mapStart = d[3] | (d[4] << 8);
            int mapLength = d[5] | (d[6] << 8);
            int mapBits = d[7];
            int width = d[12] | (d[13] << 8);
            int height = d[14] | (d[15] << 8);
            int bits = d[16];
            int descriptor = d[17];
            bool top = (descriptor & 0x20) != 0;
            bool right = (descriptor & 0x10) != 0;
            bool alpha = bits == 32 && (descriptor & 0x0F) == 8;
            raster = new CadRaster { Width = width, Height = height };

            if (!pixels)
            {
                return true;
            }

            int perPixel = (bits + 7) / 8;
            int mapEntry = (mapBits + 7) / 8;
            int at = 18 + idLength;
            int[] map = null;

            if (d[1] == 1)
            {
                map = new int[Math.Max(256, mapStart + mapLength)];

                for (int i = 0; i < mapLength && at + (i + 1) * mapEntry <= d.Length; i++)
                {
                    map[mapStart + i] = Colour(d, at + i * mapEntry, mapBits);
                }

                at += mapLength * mapEntry;
            }

            long total = (long)width * height;

            if (total > CadRasterReader.MostRawPixels)
            {
                raster = null;
                return false;
            }

            byte[] raw;

            if (type >= 9)
            {
                raw = new byte[total * perPixel];
                long written = 0;

                while (written < raw.Length && at < d.Length)
                {
                    int packet = d[at++];
                    int count = (packet & 0x7F) + 1;
                    long bytes = (long)count * perPixel;

                    if ((packet & 0x80) != 0)
                    {
                        if (at + perPixel > d.Length)
                        {
                            break;
                        }

                        for (int i = 0; i < count && written < raw.Length; i++)
                        {
                            Buffer.BlockCopy(d, at, raw, (int)written, perPixel);
                            written += perPixel;
                        }

                        at += perPixel;
                    }
                    else
                    {
                        long take = Math.Min(Math.Min(bytes, raw.Length - written), d.Length - at);

                        if (take <= 0)
                        {
                            break;
                        }

                        Buffer.BlockCopy(d, at, raw, (int)written, (int)take);
                        written += take;
                        at += (int)bytes;
                    }
                }

                at = 0;
            }
            else
            {
                raw = d;
            }

            var sink = new CadRasterSink(raster);
            var row = new byte[width * 4];

            for (int y = 0; y < height; y++)
            {
                int source = top ? y : height - 1 - y;
                long start = (long)at + (long)source * width * perPixel;

                for (int x = 0; x < width; x++)
                {
                    long p = start + (long)(right ? width - 1 - x : x) * perPixel;
                    int c;
                    int a = 255;

                    if (p + perPixel > raw.Length)
                    {
                        // A file cut short: what it lacks is transparent.
                        c = 0;
                        a = 0;
                    }
                    else if (map != null)
                    {
                        c = map[raw[p]];
                    }
                    else if (type == 3 || type == 11)
                    {
                        c = (raw[p] << 16) | (raw[p] << 8) | raw[p];
                    }
                    else
                    {
                        c = Colour(raw, (int)p, bits);

                        if (alpha)
                        {
                            a = raw[p + 3];
                        }
                    }

                    row[4 * x] = (byte)(c >> 16);
                    row[4 * x + 1] = (byte)(c >> 8);
                    row[4 * x + 2] = (byte)c;
                    row[4 * x + 3] = (byte)a;
                }

                sink.Add(row, 0);
            }

            raster = sink.Finish();
            return true;
        }

        private static bool IsMapDepth(int bits) => bits == 15 || bits == 16 || bits == 24 || bits == 32;

        /// <summary>A colour of 15 or 16 bits (5 each of blue, green and red, the top bit of 16 an attribute), 24 or 32 bits (blue, green, red, then alpha), as RGB.</summary>
        private static int Colour(byte[] d, int at, int bits)
        {
            if (bits == 15 || bits == 16)
            {
                int v = d[at] | (d[at + 1] << 8);
                int b = v & 31;
                int g = (v >> 5) & 31;
                int r = (v >> 10) & 31;
                return (((r * 255 + 15) / 31) << 16) | (((g * 255 + 15) / 31) << 8) | ((b * 255 + 15) / 31);
            }

            return (d[at + 2] << 16) | (d[at + 1] << 8) | d[at];
        }
    }
}
