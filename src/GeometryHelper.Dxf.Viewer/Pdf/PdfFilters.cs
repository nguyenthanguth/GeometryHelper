using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The filters of a PDF stream that turn bytes into bytes: Flate (with the PNG and TIFF predictors), LZW, ASCII85, ASCIIHex and
    /// run length. The image filters (DCT, JPX, CCITT, JBIG2) are left to the picture code; a stream that ends in one is
    /// given up to it undecoded.
    /// </summary>
    internal static class PdfFilters
    {
        /// <summary>The most a stream may inflate to: 400 million bytes, which a page of a picture a metre wide does not reach.</summary>
        private const int MostBytes = 400 * 1024 * 1024;

        /// <summary>Whether a filter is one of the picture filters, whose output is a picture file or a bitmap the filter makes.</summary>
        public static bool IsImageFilter(string name) =>
            name == "DCTDecode"
            || name == "DCT"
            || name == "JPXDecode"
            || name == "CCITTFaxDecode"
            || name == "CCF"
            || name == "JBIG2Decode";

        /// <summary>Applies one filter that works on bytes alone.</summary>
        /// <param name="name">The filter's name.</param>
        /// <param name="data">The bytes.</param>
        /// <param name="parameters">Its decode parameters, or null.</param>
        /// <returns>The decoded bytes.</returns>
        public static byte[] Apply(string name, byte[] data, PdfDict parameters)
        {
            switch (name)
            {
                case "FlateDecode":
                case "Fl":
                    return Predicted(Inflate(data), parameters);
                case "LZWDecode":
                case "LZW":
                    return Predicted(
                        Lzw(
                            data,
                            parameters == null
                                || !(parameters.TryGetValue("EarlyChange", out object e))
                                || !(e is double d)
                                || d != 0.0
                        ),
                        parameters
                    );
                case "ASCII85Decode":
                case "A85":
                    return Ascii85(data);
                case "ASCIIHexDecode":
                case "AHx":
                    return AsciiHex(data);
                case "RunLengthDecode":
                case "RL":
                    return RunLength(data);
                case "Crypt":
                    return data;
                default:
                    throw new PdfException("The filter " + name + " is not one the reader decodes.");
            }
        }

        /// <summary>Inflates zlib or raw deflate data; what could be inflated of data cut short.</summary>
        public static byte[] Inflate(byte[] data)
        {
            int skip = data.Length >= 2 && (data[0] & 0x0F) == 8 && ((data[0] << 8) | data[1]) % 31 == 0 ? 2 : 0;
            var output = new MemoryStream();

            try
            {
                using (var input = new MemoryStream(data, skip, data.Length - skip, false))
                using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    var buffer = new byte[16384];
                    int read;

                    while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);

                        if (output.Length > MostBytes)
                        {
                            throw new PdfException("A stream inflates to more than 400 million bytes.");
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
                // A stream cut short or damaged gives what was inflated before the fault.
            }

            return output.ToArray();
        }

        private static byte[] Predicted(byte[] data, PdfDict parameters)
        {
            if (
                parameters == null
                || !parameters.TryGetValue("Predictor", out object p)
                || !(p is double predictor)
                || predictor < 2
            )
            {
                return data;
            }

            int colors = Whole(parameters, "Colors", 1);
            int bits = Whole(parameters, "BitsPerComponent", 8);
            int columns = Whole(parameters, "Columns", 1);
            int bytesPerPixel = Math.Max(1, (colors * bits + 7) / 8);
            int row = (colors * bits * columns + 7) / 8;

            if (row <= 0)
            {
                return data;
            }

            if (predictor == 2.0)
            {
                if (bits == 8)
                {
                    for (int start = 0; start + row <= data.Length; start += row)
                    {
                        for (int i = bytesPerPixel; i < row; i++)
                        {
                            data[start + i] = (byte)(data[start + i] + data[start + i - bytesPerPixel]);
                        }
                    }
                }

                return data;
            }

            int rows = (data.Length + row) / (row + 1);
            var output = new byte[rows * row];
            var previous = new byte[row];
            var line = new byte[row];

            for (int r = 0; r < rows; r++)
            {
                int at = r * (row + 1);

                if (at >= data.Length)
                {
                    break;
                }

                int type = data[at];
                int have = Math.Min(row, data.Length - at - 1);
                Array.Clear(line, 0, row);
                Buffer.BlockCopy(data, at + 1, line, 0, Math.Max(0, have));

                for (int i = 0; i < row; i++)
                {
                    int left = i >= bytesPerPixel ? line[i - bytesPerPixel] : 0;
                    int up = previous[i];
                    int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                    int value = line[i];

                    switch (type)
                    {
                        case 1:
                            value += left;
                            break;
                        case 2:
                            value += up;
                            break;
                        case 3:
                            value += (left + up) / 2;
                            break;
                        case 4:
                            value += Paeth(left, up, upLeft);
                            break;
                    }

                    line[i] = (byte)value;
                }

                Buffer.BlockCopy(line, 0, output, r * row, row);
                Buffer.BlockCopy(line, 0, previous, 0, row);
            }

            return output;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a
                : pb <= pc ? b
                : c;
        }

        private static int Whole(PdfDict dictionary, string key, int fallback) =>
            dictionary.TryGetValue(key, out object value) && value is double number ? (int)number : fallback;

        private static byte[] Lzw(byte[] data, bool early)
        {
            var output = new MemoryStream();
            var table = new List<byte[]>(4096);
            int bitsLeft = 0;
            int buffer = 0;
            int position = 0;
            int width = 9;
            byte[] previous = null;

            void Reset()
            {
                table.Clear();

                for (int i = 0; i < 256; i++)
                {
                    table.Add(new[] { (byte)i });
                }

                table.Add(null);
                table.Add(null);
                width = 9;
                previous = null;
            }

            Reset();

            while (true)
            {
                while (bitsLeft < width && position < data.Length)
                {
                    buffer = (buffer << 8) | data[position++];
                    bitsLeft += 8;
                }

                if (bitsLeft < width)
                {
                    break;
                }

                int code = (buffer >> (bitsLeft - width)) & ((1 << width) - 1);
                bitsLeft -= width;
                buffer &= (1 << bitsLeft) - 1;

                if (code == 256)
                {
                    Reset();
                    continue;
                }

                if (code == 257)
                {
                    break;
                }

                byte[] entry;

                if (code < table.Count && table[code] != null)
                {
                    entry = table[code];
                }
                else if (previous != null && code == table.Count)
                {
                    entry = new byte[previous.Length + 1];
                    Buffer.BlockCopy(previous, 0, entry, 0, previous.Length);
                    entry[previous.Length] = previous[0];
                }
                else
                {
                    break;
                }

                output.Write(entry, 0, entry.Length);

                if (previous != null && table.Count < 4096)
                {
                    var added = new byte[previous.Length + 1];
                    Buffer.BlockCopy(previous, 0, added, 0, previous.Length);
                    added[previous.Length] = entry[0];
                    table.Add(added);
                }

                previous = entry;
                int size = table.Count + (early ? 1 : 0);
                width =
                    size >= 2048 ? 12
                    : size >= 1024 ? 11
                    : size >= 512 ? 10
                    : 9;

                if (output.Length > MostBytes)
                {
                    break;
                }
            }

            return output.ToArray();
        }

        private static byte[] Ascii85(byte[] data)
        {
            var output = new MemoryStream();
            var group = new int[5];
            int count = 0;
            int i = 0;

            if (data.Length >= 2 && data[0] == '<' && data[1] == '~')
            {
                i = 2;
            }

            for (; i < data.Length; i++)
            {
                int c = data[i];

                if (PdfParser.IsWhite(c))
                {
                    continue;
                }

                if (c == '~')
                {
                    break;
                }

                if (c == 'z' && count == 0)
                {
                    output.Write(new byte[4], 0, 4);
                    continue;
                }

                if (c < '!' || c > 'u')
                {
                    continue;
                }

                group[count++] = c - '!';

                if (count == 5)
                {
                    long value = 0;

                    for (int k = 0; k < 5; k++)
                    {
                        value = value * 85 + group[k];
                    }

                    output.WriteByte((byte)(value >> 24));
                    output.WriteByte((byte)(value >> 16));
                    output.WriteByte((byte)(value >> 8));
                    output.WriteByte((byte)value);
                    count = 0;
                }
            }

            if (count > 1)
            {
                for (int k = count; k < 5; k++)
                {
                    group[k] = 84;
                }

                long value = 0;

                for (int k = 0; k < 5; k++)
                {
                    value = value * 85 + group[k];
                }

                for (int k = 0; k < count - 1; k++)
                {
                    output.WriteByte((byte)(value >> (24 - 8 * k)));
                }
            }

            return output.ToArray();
        }

        private static byte[] AsciiHex(byte[] data)
        {
            var output = new MemoryStream();
            int high = -1;

            foreach (byte b in data)
            {
                if (b == '>')
                {
                    break;
                }

                int v =
                    b >= '0' && b <= '9' ? b - '0'
                    : b >= 'a' && b <= 'f' ? b - 'a' + 10
                    : b >= 'A' && b <= 'F' ? b - 'A' + 10
                    : -1;

                if (v < 0)
                {
                    continue;
                }

                if (high < 0)
                {
                    high = v;
                }
                else
                {
                    output.WriteByte((byte)((high << 4) | v));
                    high = -1;
                }
            }

            if (high >= 0)
            {
                output.WriteByte((byte)(high << 4));
            }

            return output.ToArray();
        }

        private static byte[] RunLength(byte[] data)
        {
            var output = new MemoryStream();
            int i = 0;

            while (i < data.Length)
            {
                int length = data[i++];

                if (length == 128)
                {
                    break;
                }

                if (length < 128)
                {
                    int n = Math.Min(length + 1, data.Length - i);
                    output.Write(data, i, n);
                    i += n;
                }
                else if (i < data.Length)
                {
                    byte value = data[i++];

                    for (int k = 0; k < 257 - length; k++)
                    {
                        output.WriteByte(value);
                    }
                }

                if (output.Length > MostBytes)
                {
                    break;
                }
            }

            return output.ToArray();
        }
    }
}
