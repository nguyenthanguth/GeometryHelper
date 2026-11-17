using System;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// ZSoft PCX (.pcx), run-length coded: 1 bit a pixel in one plane (bitonal, 0 black and 1 white), 1 to 4 bits in one plane
    /// or 1 bit in 3 or 4 planes with the palette of the header (up to 16 colours), 8 bits in one plane with the palette at
    /// the end of the file (256 colours), and 8 bits in 3 or 4 planes (true colour, with alpha for 4). Its resolution is the
    /// dots an inch of its header.
    /// </summary>
    internal static class CadPcxFile
    {
        /// <summary>Whether the bytes begin a PCX this reads: its 0x0A mark, a version, run-length coding, and a depth and planes that mean something.</summary>
        internal static bool LooksLikePcx(byte[] d)
        {
            if (d.Length < 128 || d[0] != 0x0A || d[2] != 1)
            {
                return false;
            }

            int bits = d[3];
            int planes = d[65];
            return d[1] <= 5
                && (bits == 1 || bits == 2 || bits == 4 || bits == 8)
                && planes >= 1
                && planes <= 4
                && (d[66] | (d[67] << 8)) > 0
                && Le16(d, 8) >= Le16(d, 4)
                && Le16(d, 10) >= Le16(d, 6);
        }

        /// <summary>Reads a PCX: its size and resolution, and its pixels where asked.</summary>
        /// <param name="d">The bytes of the file.</param>
        /// <param name="pixels">Whether to decode the pixels.</param>
        /// <param name="raster">The picture; null where the bytes are no PCX this reads.</param>
        internal static bool TryRead(byte[] d, bool pixels, out CadRaster raster)
        {
            raster = null;

            if (!LooksLikePcx(d))
            {
                return false;
            }

            int bits = d[3];
            int planes = d[65];
            int width = Le16(d, 8) - Le16(d, 4) + 1;
            int height = Le16(d, 10) - Le16(d, 6) + 1;
            int perLine = d[66] | (d[67] << 8);
            int dpiX = Le16(d, 12);
            int dpiY = Le16(d, 14);
            bool supported =
                (planes == 1 && bits <= 8)
                || (bits == 1 && (planes == 3 || planes == 4))
                || (bits == 8 && (planes == 3 || planes == 4));

            if (width <= 0 || height <= 0 || !supported || (double)width * height > CadRasterReader.MostRawPixels)
            {
                return false;
            }

            raster = new CadRaster
            {
                Width = width,
                Height = height,
                MillimetresX = dpiX > 0 ? 25.4 / dpiX : 0.0,
                MillimetresY = dpiY > 0 ? 25.4 / dpiY : 0.0,
                IsBitonal = bits == 1 && planes == 1,
            };

            if (!pixels)
            {
                return true;
            }

            // The palette of 256 colours is the 768 bytes after a 0x0C at the end; where there is none, the greys.
            int[] colours = new int[256];
            bool hasPalette = bits == 8 && planes == 1;
            int end = d.Length;

            if (hasPalette)
            {
                if (d.Length > 128 + 769 && d[d.Length - 769] == 0x0C)
                {
                    end = d.Length - 769;

                    for (int i = 0; i < 256; i++)
                    {
                        int p = d.Length - 768 + 3 * i;
                        colours[i] = (d[p] << 16) | (d[p + 1] << 8) | d[p + 2];
                    }
                }
                else
                {
                    for (int i = 0; i < 256; i++)
                    {
                        colours[i] = (i << 16) | (i << 8) | i;
                    }
                }
            }
            else if (bits == 1 && planes == 1)
            {
                colours[0] = 0x000000;
                colours[1] = 0xFFFFFF;
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    colours[i] = (d[16 + 3 * i] << 16) | (d[17 + 3 * i] << 8) | d[18 + 3 * i];
                }
            }

            int lineBytes = perLine * planes;
            var line = new byte[lineBytes];
            var row = new byte[width * 4];
            var sink = new CadRasterSink(raster);
            int at = 128;

            for (int y = 0; y < height; y++)
            {
                // One line of the run-length coding: a byte of 0xC0 or more is a count and the byte after it the value.
                int filled = 0;

                while (filled < lineBytes && at < end)
                {
                    int b = d[at++];

                    if ((b & 0xC0) == 0xC0)
                    {
                        int count = b & 0x3F;
                        int value = at < end ? d[at++] : 0;

                        for (int i = 0; i < count && filled < lineBytes; i++)
                        {
                            line[filled++] = (byte)value;
                        }
                    }
                    else
                    {
                        line[filled++] = (byte)b;
                    }
                }

                // A file cut short leaves the rest of the line as the first colour of the palette.
                for (int i = filled; i < lineBytes; i++)
                {
                    line[i] = 0;
                }

                for (int x = 0; x < width; x++)
                {
                    int c;
                    int a = 255;

                    if (bits == 8 && planes >= 3)
                    {
                        c = (line[x] << 16) | (line[perLine + x] << 8) | line[2 * perLine + x];

                        if (planes == 4)
                        {
                            a = line[3 * perLine + x];
                        }
                    }
                    else if (planes == 1)
                    {
                        int bit = x * bits;
                        c = colours[(line[bit >> 3] >> (8 - bits - (bit & 7))) & ((1 << bits) - 1)];
                    }
                    else
                    {
                        // Planes of one bit each: the bit of each plane makes one bit of the index into the palette.
                        int index = 0;

                        for (int p = 0; p < planes; p++)
                        {
                            index |= ((line[p * perLine + (x >> 3)] >> (7 - (x & 7))) & 1) << p;
                        }

                        c = colours[index];
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

        private static int Le16(byte[] d, int at) => d[at] | (d[at + 1] << 8);
    }
}
