using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The size and resolution of a picture file read from its head, as AutoCAD reads them when it attaches an image: a PNG
    /// (its IHDR and pHYs), a JPEG (its frame and JFIF density), a BMP, DIB or RLE (its info header), and a GIF, PCX, TGA or
    /// TIFF (<see cref="CadRasterReader"/>). Whether the picture is bitonal, one bit a pixel, is read with them. No pixel is
    /// decoded.
    /// </summary>
    internal static class CadImageFile
    {
        // The pictures already measured, by file and the time it was written, so that a scan of 30 MB is read once.
        private static readonly Dictionary<string, bool> Bitonals = new Dictionary<string, bool>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>
        /// The filter of the dialogs that choose an image file, as AutoCAD's Select Image File dialog groups the types: all of
        /// them first, then each by name.
        /// </summary>
        internal const string OpenFilter =
            "All image files|*.bmp;*.rle;*.dib;*.gif;*.jpg;*.jpeg;*.pcx;*.png;*.tga;*.tif;*.tiff"
            + "|BMP, RLE, DIB (*.bmp;*.rle;*.dib)|*.bmp;*.rle;*.dib|GIF (*.gif)|*.gif|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg"
            + "|PCX (*.pcx)|*.pcx|PNG (*.png)|*.png|TGA (*.tga)|*.tga|TIFF (*.tif;*.tiff)|*.tif;*.tiff";

        /// <summary>
        /// Whether a path names a picture the presenter attaches: .bmp, .rle, .dib, .gif, .jpg, .jpeg, .pcx, .png, .tga, .tif or
        /// .tiff.
        /// </summary>
        internal static bool IsPicture(string path) => CadRasterReader.IsPictureName(path);

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
            bool read = TryDescribe(path, out CadRaster picture);
            width = read ? picture.Width : 0;
            height = read ? picture.Height : 0;
            millimetresX = read ? picture.MillimetresX : 0.0;
            millimetresY = read ? picture.MillimetresY : 0.0;
            return read;
        }

        /// <summary>
        /// Whether a picture file is bitonal, one bit a pixel and one plane (a scanned drawing): the image of one cannot be
        /// adjusted, and it is shown in the colour of the image object. False for a file that cannot be read.
        /// </summary>
        internal static bool IsBitonal(string path)
        {
            if (path == null)
            {
                return false;
            }

            string key;

            try
            {
                key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
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

            lock (Bitonals)
            {
                if (Bitonals.TryGetValue(key, out bool known))
                {
                    return known;
                }
            }

            bool bitonal = TryDescribe(path, out CadRaster picture) && picture.IsBitonal;

            lock (Bitonals)
            {
                Bitonals[key] = bitonal;
            }

            return bitonal;
        }

        /// <summary>Reads the size, resolution and kind of a picture file from its head; false where it is none this reads.</summary>
        internal static bool TryDescribe(string path, out CadRaster picture)
        {
            picture = null;
            byte[] head;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // The head of a JPEG may lie behind large EXIF data: up to 1 MB is read. A TIFF keeps its directory
                    // where the writer left it, often at the end, so all of it is read.
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    long most = extension == ".tif" || extension == ".tiff" ? 1L << 30 : 1 << 20;
                    head = new byte[(int)Math.Min(stream.Length, most)];
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

            int width = 0;
            int height = 0;
            double mx = 0.0;
            double my = 0.0;
            bool bitonal = false;
            bool read2 =
                Png(head, ref width, ref height, ref mx, ref my, ref bitonal)
                || Jpeg(head, ref width, ref height, ref mx, ref my);

            if (read2 && width > 0 && height > 0)
            {
                picture = new CadRaster
                {
                    Width = width,
                    Height = height,
                    MillimetresX = mx,
                    MillimetresY = my,
                    IsBitonal = bitonal,
                };
                return true;
            }

            // The others are read by their own decoders, for the header alone.
            return CadRasterReader.TryRead(head, path, false, out picture) && picture.Width > 0 && picture.Height > 0;
        }

        private static bool Png(byte[] b, ref int width, ref int height, ref double mx, ref double my, ref bool bitonal)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            if (b.Length < 26)
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

            // One bit a pixel: a grey or palette picture of bit depth 1.
            bitonal = b[24] == 1 && (b[25] == 0 || b[25] == 3);
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

        private static int BigEndian(byte[] b, int at) =>
            (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
    }
}
