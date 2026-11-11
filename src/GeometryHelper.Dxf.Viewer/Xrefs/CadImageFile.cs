using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The size and resolution of a picture file read from its head, as AutoCAD reads them when it attaches an image: a PNG
    /// (its IHDR and pHYs), a JPEG (its frame and JFIF density) or a BMP (its info header). No pixel is decoded.
    /// </summary>
    internal static class CadImageFile
    {
        /// <summary>Whether a path names a picture the presenter attaches: .png, .jpg, .jpeg or .bmp.</summary>
        internal static bool IsPicture(string path)
        {
            string extension;

            try
            {
                extension = Path.GetExtension(path ?? string.Empty);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the pixels across and up of a picture and the millimetres a pixel is across and up, nought where the file
        /// gives no resolution: a PNG of 40 by 30 pixels saved at 3 779 pixels a metre is 0.26462 mm a pixel.
        /// </summary>
        internal static bool TryRead(
            string path,
            out int width,
            out int height,
            out double millimetresX,
            out double millimetresY
        )
        {
            width = 0;
            height = 0;
            millimetresX = 0.0;
            millimetresY = 0.0;
            byte[] head;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // The head of a JPEG may lie behind large EXIF data: up to 1 MB is read.
                    head = new byte[(int)Math.Min(stream.Length, 1 << 20)];
                    int read = 0;

                    while (read < head.Length)
                    {
                        int n = stream.Read(head, read, head.Length - read);

                        if (n <= 0)
                        {
                            break;
                        }

                        read += n;
                    }
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                return false;
            }

            bool read2 =
                Png(head, ref width, ref height, ref millimetresX, ref millimetresY)
                || Jpeg(head, ref width, ref height, ref millimetresX, ref millimetresY)
                || Bmp(head, ref width, ref height, ref millimetresX, ref millimetresY);
            return read2 && width > 0 && height > 0;
        }

        private static bool Png(byte[] b, ref int width, ref int height, ref double mx, ref double my)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            if (b.Length < 24)
            {
                return false;
            }

            for (int i = 0; i < signature.Length; i++)
            {
                if (b[i] != signature[i])
                {
                    return false;
                }
            }

            width = BigEndian(b, 16);
            height = BigEndian(b, 20);
            int at = 8;

            while (at + 8 <= b.Length)
            {
                int length = BigEndian(b, at);
                string type = System.Text.Encoding.ASCII.GetString(b, at + 4, 4);

                if (length < 0 || type == "IDAT" || type == "IEND")
                {
                    break;
                }

                // pHYs: pixels a unit across and up, the unit a metre where its byte is 1.
                if (type == "pHYs" && length >= 9 && at + 17 <= b.Length && b[at + 16] == 1)
                {
                    int x = BigEndian(b, at + 8);
                    int y = BigEndian(b, at + 12);
                    mx = x > 0 ? 1000.0 / x : 0.0;
                    my = y > 0 ? 1000.0 / y : 0.0;
                }

                at += 12 + length;
            }

            return true;
        }

        private static bool Jpeg(byte[] b, ref int width, ref int height, ref double mx, ref double my)
        {
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8)
            {
                return false;
            }

            int at = 2;

            while (at + 4 <= b.Length)
            {
                if (b[at] != 0xFF)
                {
                    return false;
                }

                byte marker = b[at + 1];

                if (marker == 0xFF)
                {
                    at++;
                    continue;
                }

                int length = (b[at + 2] << 8) | b[at + 3];

                // JFIF: its density, in dots an inch (1) or a centimetre (2); 0 gives the aspect alone.
                if (
                    marker == 0xE0
                    && length >= 14
                    && at + 16 <= b.Length
                    && b[at + 4] == 'J'
                    && b[at + 5] == 'F'
                    && b[at + 6] == 'I'
                    && b[at + 7] == 'F'
                )
                {
                    int units = b[at + 11];
                    int x = (b[at + 12] << 8) | b[at + 13];
                    int y = (b[at + 14] << 8) | b[at + 15];
                    double per =
                        units == 1 ? 25.4
                        : units == 2 ? 10.0
                        : 0.0;
                    mx = per > 0.0 && x > 0 ? per / x : 0.0;
                    my = per > 0.0 && y > 0 ? per / y : 0.0;
                }

                // A start of frame, not of the Huffman (C4), arithmetic (CC) tables or JPG (C8): its height, then width.
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (at + 9 > b.Length)
                    {
                        return false;
                    }

                    height = (b[at + 5] << 8) | b[at + 6];
                    width = (b[at + 7] << 8) | b[at + 8];
                    return true;
                }

                at += 2 + length;
            }

            return false;
        }

        private static bool Bmp(byte[] b, ref int width, ref int height, ref double mx, ref double my)
        {
            if (b.Length < 26 || b[0] != 'B' || b[1] != 'M')
            {
                return false;
            }

            int header = LittleEndian(b, 14);

            if (header == 12)
            {
                width = b[18] | (b[19] << 8);
                height = b[20] | (b[21] << 8);
                return true;
            }

            if (b.Length < 46)
            {
                return false;
            }

            width = LittleEndian(b, 18);
            height = Math.Abs(LittleEndian(b, 22));
            int x = LittleEndian(b, 38);
            int y = LittleEndian(b, 42);
            mx = x > 0 ? 1000.0 / x : 0.0;
            my = y > 0 ? 1000.0 / y : 0.0;
            return true;
        }

        private static int BigEndian(byte[] b, int at) =>
            (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        private static int LittleEndian(byte[] b, int at) =>
            b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
    }
}
