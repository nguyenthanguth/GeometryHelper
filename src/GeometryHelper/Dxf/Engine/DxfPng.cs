using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Reads a PNG into a .bmp of 32 bits a pixel, for <see cref="DxfOle2Frame"/>, as a picture pasted in AutoCAD is held: every
    /// kind of PNG (grey, true colour, a palette, with or without alpha, 1 to 16 bits a sample, interlaced or not), its alpha
    /// laid over white, as a picture with no alpha shows it on paper; its pixels a metre from its pHYs where it gives them in
    /// metres. No library: the image data is inflated by <see cref="DeflateStream"/>.
    /// </summary>
    internal static class DxfPng
    {
        // A picture of more pixels is refused rather than read into memory: 64 million is 256 MB of pixels.
        private const long MostPixels = 64L * 1024 * 1024;

        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>Whether bytes begin as a PNG does.</summary>
        internal static bool IsPng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Signature.Length)
            {
                return false;
            }

            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[i] != Signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The .bmp of a PNG; null, with the reason, where the PNG is damaged or of a kind PNG does not have.</summary>
        internal static byte[] ToBitmap(byte[] png, out string why)
        {
            why = null;

            try
            {
                return Read(png, out why);
            }
            catch (Exception e)
                when (e is InvalidDataException
                    || e is IOException
                    || e is IndexOutOfRangeException
                    || e is ArgumentException
                    || e is OverflowException
                )
            {
                why = "its image data cannot be read (" + e.Message.TrimEnd('.') + ")";
                return null;
            }
        }

        private static byte[] Read(byte[] png, out string why)
        {
            why = null;
            int at = Signature.Length;
            int width = 0,
                height = 0,
                depth = 0,
                colour = -1,
                interlace = 0;
            byte[] palette = null;
            byte[] transparency = null;
            int perMetreX = 0,
                perMetreY = 0;
            var data = new MemoryStream();

            while (at + 8 <= png.Length)
            {
                int length = BigEndian(png, at);
                string type = new string(
                    new[] { (char)png[at + 4], (char)png[at + 5], (char)png[at + 6], (char)png[at + 7] }
                );
                int start = at + 8;

                if (length < 0 || start + (long)length + 4 > png.Length)
                {
                    why = "its chunk " + type + " runs past its end";
                    return null;
                }

                switch (type)
                {
                    case "IHDR":
                        width = BigEndian(png, start);
                        height = BigEndian(png, start + 4);
                        depth = png[start + 8];
                        colour = png[start + 9];
                        interlace = png[start + 12];
                        break;
                    case "PLTE":
                        palette = Slice(png, start, length);
                        break;
                    case "tRNS":
                        transparency = Slice(png, start, length);
                        break;
                    case "pHYs":
                        if (length >= 9 && png[start + 8] == 1)
                        {
                            perMetreX = BigEndian(png, start);
                            perMetreY = BigEndian(png, start + 4);
                        }

                        break;
                    case "IDAT":
                        data.Write(png, start, length);
                        break;
                }

                at = start + length + 4;

                if (type == "IEND")
                {
                    break;
                }
            }

            int channels =
                colour == 0 ? 1
                : colour == 2 ? 3
                : colour == 3 ? 1
                : colour == 4 ? 2
                : colour == 6 ? 4
                : 0;
            bool depthTaken =
                colour == 0 ? depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16
                : colour == 3 ? depth == 1 || depth == 2 || depth == 4 || depth == 8
                : depth == 8 || depth == 16;

            if (channels == 0 || !depthTaken || width <= 0 || height <= 0 || interlace > 1)
            {
                why =
                    "its header gives "
                    + width
                    + " by "
                    + height
                    + " pixels of colour type "
                    + colour
                    + " and "
                    + depth
                    + " bits, interlace "
                    + interlace
                    + ", not a PNG's";
                return null;
            }

            if ((long)width * height > MostPixels)
            {
                why =
                    "its "
                    + width
                    + " by "
                    + height
                    + " pixels are more than the "
                    + MostPixels
                    + " a picture is read of";
                return null;
            }

            if (colour == 3 && palette == null)
            {
                why = "it has no palette for its colours";
                return null;
            }

            byte[] raw = Inflate(data.ToArray());
            var rgba = new byte[(long)width * height * 4];
            int bitsPerPixel = channels * depth;
            int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
            int used = 0;

            if (interlace == 0)
            {
                used = Pass(
                    raw,
                    used,
                    width,
                    height,
                    bitsPerPixel,
                    bytesPerPixel,
                    (x, y, read) => Put(rgba, width, x, y, read),
                    colour,
                    depth,
                    palette,
                    transparency
                );
            }
            else
            {
                int[] x0 = { 0, 4, 0, 2, 0, 1, 0 };
                int[] y0 = { 0, 0, 4, 0, 2, 0, 1 };
                int[] dx = { 8, 8, 4, 4, 2, 2, 1 };
                int[] dy = { 8, 8, 8, 4, 4, 2, 2 };

                for (int p = 0; p < 7; p++)
                {
                    int w = (width - x0[p] + dx[p] - 1) / dx[p];
                    int h = (height - y0[p] + dy[p] - 1) / dy[p];

                    if (w <= 0 || h <= 0)
                    {
                        continue;
                    }

                    int pass = p;
                    used = Pass(
                        raw,
                        used,
                        w,
                        h,
                        bitsPerPixel,
                        bytesPerPixel,
                        (x, y, read) => Put(rgba, width, x0[pass] + x * dx[pass], y0[pass] + y * dy[pass], read),
                        colour,
                        depth,
                        palette,
                        transparency
                    );
                }
            }

            if (used < 0)
            {
                why = "its image data is shorter than its " + width + " by " + height + " pixels";
                return null;
            }

            return Bitmap(rgba, width, height, perMetreX, perMetreY);
        }

        /// <summary>Unfilters and reads one image (or one pass of an interlaced one) of the raw data from <paramref name="at"/>; where the data ends, or -1 where it is short.</summary>
        private static int Pass(
            byte[] raw,
            int at,
            int width,
            int height,
            int bitsPerPixel,
            int bytesPerPixel,
            Action<int, int, uint> put,
            int colour,
            int depth,
            byte[] palette,
            byte[] transparency
        )
        {
            int stride = (int)(((long)width * bitsPerPixel + 7) / 8);
            var previous = new byte[stride];
            var line = new byte[stride];

            for (int y = 0; y < height; y++)
            {
                if (at + 1 + stride > raw.Length)
                {
                    return -1;
                }

                int filter = raw[at];
                Array.Copy(raw, at + 1, line, 0, stride);
                at += 1 + stride;
                Unfilter(filter, line, previous, bytesPerPixel);

                for (int x = 0; x < width; x++)
                {
                    put(x, y, Pixel(line, x, colour, depth, palette, transparency));
                }

                byte[] swap = previous;
                previous = line;
                line = swap;
            }

            return at;
        }

        private static void Unfilter(int filter, byte[] line, byte[] previous, int bpp)
        {
            for (int i = 0; i < line.Length; i++)
            {
                int left = i >= bpp ? line[i - bpp] : 0;
                int up = previous[i];
                int upLeft = i >= bpp ? previous[i - bpp] : 0;

                switch (filter)
                {
                    case 0:
                        break;
                    case 1:
                        line[i] = (byte)(line[i] + left);
                        break;
                    case 2:
                        line[i] = (byte)(line[i] + up);
                        break;
                    case 3:
                        line[i] = (byte)(line[i] + ((left + up) >> 1));
                        break;
                    case 4:
                        int p = left + up - upLeft;
                        int pa = Math.Abs(p - left),
                            pb = Math.Abs(p - up),
                            pc = Math.Abs(p - upLeft);
                        line[i] = (byte)(
                            line[i]
                            + (
                                pa <= pb && pa <= pc ? left
                                : pb <= pc ? up
                                : upLeft
                            )
                        );
                        break;
                    default:
                        throw new InvalidDataException("a line is of filter " + filter + ", which PNG has not");
                }
            }
        }

        /// <summary>The pixel at <paramref name="x"/> of a line, as red, green, blue and alpha in one number (red in the lowest byte).</summary>
        private static uint Pixel(byte[] line, int x, int colour, int depth, byte[] palette, byte[] transparency)
        {
            int Sample(int index)
            {
                if (depth == 16)
                {
                    return line[2 * index];
                }

                if (depth == 8)
                {
                    return line[index];
                }

                int perByte = 8 / depth;
                int value = (line[index / perByte] >> (8 - depth * (index % perByte + 1))) & ((1 << depth) - 1);
                return colour == 3 ? value : value * 255 / ((1 << depth) - 1);
            }

            int Raw16(int index) => depth == 16 ? (line[2 * index] << 8) | line[2 * index + 1] : -1;

            int r,
                g,
                b,
                a = 255;

            switch (colour)
            {
                case 0:
                    r = g = b = Sample(x);
                    if (transparency != null && transparency.Length >= 2)
                    {
                        int key = (transparency[0] << 8) | transparency[1];
                        int raw =
                            depth == 16 ? Raw16(x)
                            : depth == 8 ? line[x]
                            : (int)((line[x * depth / 8] >> (8 - depth * (x % (8 / depth) + 1))) & ((1 << depth) - 1));
                        a = raw == key ? 0 : 255;
                    }

                    break;
                case 2:
                    r = Sample(3 * x);
                    g = Sample(3 * x + 1);
                    b = Sample(3 * x + 2);
                    if (transparency != null && transparency.Length >= 6)
                    {
                        bool keyed =
                            depth == 16
                                ? Raw16(3 * x) == ((transparency[0] << 8) | transparency[1])
                                    && Raw16(3 * x + 1) == ((transparency[2] << 8) | transparency[3])
                                    && Raw16(3 * x + 2) == ((transparency[4] << 8) | transparency[5])
                                : r == transparency[1] && g == transparency[3] && b == transparency[5];
                        a = keyed ? 0 : 255;
                    }

                    break;
                case 3:
                    int index = Sample(x);
                    if (3 * index + 2 >= palette.Length)
                    {
                        throw new InvalidDataException(
                            "a pixel names colour " + index + " of a palette of " + palette.Length / 3
                        );
                    }

                    r = palette[3 * index];
                    g = palette[3 * index + 1];
                    b = palette[3 * index + 2];
                    a = transparency != null && index < transparency.Length ? transparency[index] : 255;
                    break;
                case 4:
                    r = g = b = Sample(2 * x);
                    a = Sample(2 * x + 1);
                    break;
                default:
                    r = Sample(4 * x);
                    g = Sample(4 * x + 1);
                    b = Sample(4 * x + 2);
                    a = Sample(4 * x + 3);
                    break;
            }

            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        private static void Put(byte[] rgba, int width, int x, int y, uint pixel)
        {
            long at = ((long)y * width + x) * 4;
            rgba[at] = (byte)pixel;
            rgba[at + 1] = (byte)(pixel >> 8);
            rgba[at + 2] = (byte)(pixel >> 16);
            rgba[at + 3] = (byte)(pixel >> 24);
        }

        /// <summary>A .bmp of 32 bits a pixel, BI_RGB, its rows from the bottom, each pixel laid over white by its alpha.</summary>
        private static byte[] Bitmap(byte[] rgba, int width, int height, int perMetreX, int perMetreY)
        {
            long pixels = (long)width * height * 4;
            var bmp = new byte[54 + pixels];
            bmp[0] = (byte)'B';
            bmp[1] = (byte)'M';
            Put32(bmp, 2, (int)(54 + pixels));
            Put32(bmp, 10, 54);
            Put32(bmp, 14, 40);
            Put32(bmp, 18, width);
            Put32(bmp, 22, height);
            bmp[26] = 1;
            bmp[28] = 32;
            Put32(bmp, 34, (int)pixels);
            Put32(bmp, 38, perMetreX > 0 ? perMetreX : 3780);
            Put32(bmp, 42, perMetreY > 0 ? perMetreY : 3780);

            for (int y = 0; y < height; y++)
            {
                long from = (long)y * width * 4;
                long to = 54 + (long)(height - 1 - y) * width * 4;

                for (int x = 0; x < width; x++, from += 4, to += 4)
                {
                    int a = rgba[from + 3];
                    bmp[to] = Over(rgba[from + 2], a);
                    bmp[to + 1] = Over(rgba[from + 1], a);
                    bmp[to + 2] = Over(rgba[from], a);
                }
            }

            return bmp;
        }

        /// <summary>A sample laid over white by an alpha, rounded.</summary>
        private static byte Over(int sample, int alpha) => (byte)((sample * alpha + 255 * (255 - alpha) + 127) / 255);

        private static byte[] Inflate(byte[] zlib)
        {
            if (zlib.Length < 2 || (zlib[0] & 0x0F) != 8 || ((zlib[0] << 8) | zlib[1]) % 31 != 0)
            {
                throw new InvalidDataException("its image data is not a zlib stream of deflate");
            }

            using (var input = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                inflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static int BigEndian(byte[] b, int at) =>
            (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        private static byte[] Slice(byte[] b, int at, int length)
        {
            var slice = new byte[length];
            Array.Copy(b, at, slice, 0, length);
            return slice;
        }

        private static void Put32(byte[] b, int at, int value)
        {
            b[at] = (byte)value;
            b[at + 1] = (byte)(value >> 8);
            b[at + 2] = (byte)(value >> 16);
            b[at + 3] = (byte)(value >> 24);
        }
    }
}
