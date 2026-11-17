using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The pictures of a PDF page turned into files the canvas decodes: a JPEG stays the JPEG it is, any other picture (raw
    /// samples of 1, 2, 4, 8 or 16 bits, in any of the colour spaces the reader knows, with its soft mask or stencil mask) is
    /// written as a PNG of 8-bit red, green, blue and alpha. A picture of a kind the reader does not decode (JPEG 2000,
    /// JBIG2, CCITT fax, a JPEG in CMYK) gives null and the page notes it was left out.
    /// </summary>
    internal static class PdfImages
    {
        /// <summary>The most pixels a picture may have: a picture of more is left out rather than read into memory.</summary>
        private const long MostPixels = 40_000_000;

        /// <summary>
        /// Makes the file of a picture.
        /// </summary>
        /// <param name="file">The PDF.</param>
        /// <param name="dictionary">The picture's dictionary (its stream's, or the inline picture's with the keys spelled out).</param>
        /// <param name="data">The picture's bytes with the filters up to a picture filter undone.</param>
        /// <param name="imageFilter">The picture filter the bytes are still in, or null.</param>
        /// <param name="resources">The resources colour spaces are looked up in.</param>
        /// <param name="fill">The colour a stencil mask paints, as 0xRRGGBB.</param>
        /// <param name="why">Why the picture was left out, where it was.</param>
        /// <returns>The file's bytes, or null.</returns>
        public static byte[] Make(
            PdfFile file,
            PdfDict dictionary,
            byte[] data,
            string imageFilter,
            PdfDict resources,
            int fill,
            out string why
        )
        {
            why = null;
            int width = (int)file.Number(Get(file, dictionary, "Width", "W"));
            int height = (int)file.Number(Get(file, dictionary, "Height", "H"));
            bool mask = Get(file, dictionary, "ImageMask", "IM") is bool m && m;

            if (width <= 0 || height <= 0 || (long)width * height > MostPixels)
            {
                why = "very large pictures";
                return null;
            }

            object spaceSpec = Get(file, dictionary, "ColorSpace", "CS");
            PdfColorSpace space =
                mask ? PdfColorSpace.Gray
                : spaceSpec == null ? PdfColorSpace.Gray
                : PdfColorSpace.Parse(file, spaceSpec, resources);

            if (imageFilter != null)
            {
                if ((imageFilter == "DCTDecode" || imageFilter == "DCT") && space.Components != 4 && !mask)
                {
                    return data;
                }

                why =
                    imageFilter == "DCTDecode" || imageFilter == "DCT" ? "CMYK JPEG pictures"
                    : imageFilter == "CCITTFaxDecode" || imageFilter == "CCF" ? "fax (CCITT) pictures"
                    : imageFilter == "JPXDecode" ? "JPEG 2000 pictures"
                    : "JBIG2 pictures";
                return null;
            }

            int bits = mask ? 1 : (int)file.Number(Get(file, dictionary, "BitsPerComponent", "BPC"), 8);

            if (bits != 1 && bits != 2 && bits != 4 && bits != 8 && bits != 16)
            {
                why = "pictures of an odd depth";
                return null;
            }

            int n = space.Components;

            if (n <= 0)
            {
                why = "pictures in a pattern colour space";
                return null;
            }

            double[] decode = file.Numbers(Get(file, dictionary, "Decode", "D"));
            var rgba = new byte[width * height * 4];
            int rowBytes = (width * n * bits + 7) / 8;
            double max = Math.Pow(2.0, bits) - 1.0;
            bool indexed = space.IsIndexed;
            var sample = new double[n];
            var cache = new Dictionary<long, int>();

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    int at = (y * width + x) * 4;

                    if (mask)
                    {
                        int bit = BitAt(data, rowStart, x, 1);
                        bool paint =
                            decode != null && decode.Length >= 2 && decode[0] > decode[1] ? bit == 1 : bit == 0;

                        rgba[at] = (byte)(fill >> 16);
                        rgba[at + 1] = (byte)(fill >> 8);
                        rgba[at + 2] = (byte)fill;
                        rgba[at + 3] = paint ? (byte)255 : (byte)0;
                        continue;
                    }

                    long key = 0;

                    for (int c = 0; c < n; c++)
                    {
                        int raw = BitAt(data, rowStart, x * n + c, bits);
                        key = (key << 16) ^ raw;
                        double d0 = decode != null && 2 * c + 1 < decode.Length ? decode[2 * c] : 0.0;
                        double d1 =
                            decode != null && 2 * c + 1 < decode.Length ? decode[2 * c + 1]
                            : indexed ? max
                            : 1.0;
                        sample[c] = indexed ? d0 + raw * (d1 - d0) / max : d0 + raw / max * (d1 - d0);
                    }

                    int rgb;

                    if (n <= 3 && bits <= 8)
                    {
                        if (!cache.TryGetValue(key, out rgb))
                        {
                            rgb = space.ToRgb(sample);

                            if (cache.Count < 4096)
                            {
                                cache[key] = rgb;
                            }
                        }
                    }
                    else
                    {
                        rgb = space.ToRgb(sample);
                    }

                    rgba[at] = (byte)(rgb >> 16);
                    rgba[at + 1] = (byte)(rgb >> 8);
                    rgba[at + 2] = (byte)rgb;
                    rgba[at + 3] = 255;
                }
            }

            ApplyMasks(file, dictionary, rgba, width, height, resources);
            return Png(rgba, width, height);
        }

        private static object Get(PdfFile file, PdfDict dictionary, string key, string shortKey) =>
            file.Get(dictionary, key) ?? file.Get(dictionary, shortKey);

        private static int BitAt(byte[] data, int rowStart, int index, int bits)
        {
            switch (bits)
            {
                case 8:
                    return rowStart + index < data.Length ? data[rowStart + index] : 0;
                case 16:
                    return rowStart + 2 * index + 1 < data.Length
                        ? (data[rowStart + 2 * index] << 8) | data[rowStart + 2 * index + 1]
                        : 0;
                default:
                    int bit = index * bits;
                    int at = rowStart + bit / 8;

                    if (at >= data.Length)
                    {
                        return 0;
                    }

                    int shift = 8 - bits - bit % 8;
                    return (data[at] >> shift) & ((1 << bits) - 1);
            }
        }

        /// <summary>The soft mask or stencil mask of a picture, laid into its alpha.</summary>
        private static void ApplyMasks(
            PdfFile file,
            PdfDict dictionary,
            byte[] rgba,
            int width,
            int height,
            PdfDict resources
        )
        {
            PdfStream soft = file.Stream(file.Get(dictionary, "SMask"));
            PdfStream stencil = soft == null ? file.Stream(file.Get(dictionary, "Mask")) : null;
            PdfStream source = soft ?? stencil;

            if (source == null)
            {
                return;
            }

            try
            {
                byte[] bytes = file.Decode(source, out string filter, out _);

                if (filter != null)
                {
                    return;
                }

                PdfDict d = source.Dictionary;
                int mw = (int)file.Number(file.Get(d, "Width"));
                int mh = (int)file.Number(file.Get(d, "Height"));
                int bits = soft != null ? (int)file.Number(file.Get(d, "BitsPerComponent"), 8) : 1;

                if (mw <= 0 || mh <= 0 || (bits != 1 && bits != 2 && bits != 4 && bits != 8 && bits != 16))
                {
                    return;
                }

                double[] decode = file.Numbers(file.Get(d, "Decode"));
                bool reversed = decode != null && decode.Length >= 2 && decode[0] > decode[1];
                int rowBytes = (mw * bits + 7) / 8;
                double max = Math.Pow(2.0, bits) - 1.0;

                for (int y = 0; y < height; y++)
                {
                    int my = Math.Min(mh - 1, y * mh / height);

                    for (int x = 0; x < width; x++)
                    {
                        int mx = Math.Min(mw - 1, x * mw / width);
                        int raw = BitAt(bytes, my * rowBytes, mx, bits);
                        double v = raw / max;

                        if (reversed)
                        {
                            v = 1.0 - v;
                        }

                        // A stencil: 1 masks the sample out (transparent), 0 keeps it. A soft mask is the alpha itself.
                        double alpha = soft != null ? v : 1.0 - v;
                        int at = (y * width + x) * 4 + 3;
                        rgba[at] = (byte)Math.Max(0, Math.Min(255, (int)(alpha * rgba[at] + 0.5)));
                    }
                }
            }
            catch (PdfException)
            {
                // A mask that cannot be read leaves the picture whole.
            }
        }

        /// <summary>Writes 8-bit red, green, blue and alpha as a PNG file.</summary>
        public static byte[] Png(byte[] rgba, int width, int height)
        {
            var raw = new byte[height * (width * 4 + 1)];

            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
            }

            var zlib = new MemoryStream();
            zlib.WriteByte(0x78);
            zlib.WriteByte(0x01);

            using (var deflate = new DeflateStream(zlib, CompressionLevel.Fastest, true))
            {
                deflate.Write(raw, 0, raw.Length);
            }

            uint a = 1;
            uint b = 0;

            foreach (byte v in raw)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }

            uint adler = (b << 16) | a;
            zlib.WriteByte((byte)(adler >> 24));
            zlib.WriteByte((byte)(adler >> 16));
            zlib.WriteByte((byte)(adler >> 8));
            zlib.WriteByte((byte)adler);

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            var header = new byte[13];
            Put(header, 0, (uint)width);
            Put(header, 4, (uint)height);
            header[8] = 8;
            header[9] = 6;
            Chunk(png, "IHDR", header);
            Chunk(png, "IDAT", zlib.ToArray());
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        private static void Put(byte[] bytes, int at, uint value)
        {
            bytes[at] = (byte)(value >> 24);
            bytes[at + 1] = (byte)(value >> 16);
            bytes[at + 2] = (byte)(value >> 8);
            bytes[at + 3] = (byte)value;
        }

        private static void Chunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            Put(length, 0, (uint)data.Length);
            stream.Write(length, 0, 4);
            var body = new byte[4 + data.Length];

            for (int i = 0; i < 4; i++)
            {
                body[i] = (byte)type[i];
            }

            Buffer.BlockCopy(data, 0, body, 4, data.Length);
            stream.Write(body, 0, body.Length);
            var crc = new byte[4];
            Put(crc, 0, Crc(body));
            stream.Write(crc, 0, 4);
        }

        private static uint[] _table;

        private static uint Crc(byte[] bytes)
        {
            if (_table == null)
            {
                var table = new uint[256];

                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;

                    for (int k = 0; k < 8; k++)
                    {
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    }

                    table[n] = c;
                }

                _table = table;
            }

            uint crc = 0xFFFFFFFFu;

            foreach (byte v in bytes)
            {
                crc = _table[(crc ^ v) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }
    }
}
