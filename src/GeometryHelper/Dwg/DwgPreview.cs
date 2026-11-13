using System;
using System.IO;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The preview image of a DWG (the section <c>AcDb:Preview</c>; of AutoCAD 2000 a block of the file the file header
    /// points to): a start sentinel, the size of what follows as a raw long, a count of images, for each its kind, its place
    /// in the file and its size, the images, and an end sentinel. A DXF keeps the picture of it in the section
    /// <c>THUMBNAILIMAGE</c>. AutoCAD 2021 writes a header of 80 zero bytes (kind 1) and the bitmap of Windows without the
    /// 14 bytes of its file header (kind 2) after the list; the place of each is its place in the file, the place of the
    /// section's data and its own place in the section.
    /// </summary>
    internal static class DwgPreview
    {
        private static readonly byte[] Start =
        {
            0x1F,
            0x25,
            0x6D,
            0x07,
            0xD4,
            0x36,
            0x28,
            0x28,
            0x9D,
            0x57,
            0xCA,
            0x3F,
            0x9D,
            0x44,
            0x10,
            0x2B,
        };

        private static readonly byte[] End =
        {
            0xE0,
            0xDA,
            0x92,
            0xF8,
            0x2B,
            0xC9,
            0xD7,
            0xD7,
            0x62,
            0xA8,
            0x35,
            0xC0,
            0x62,
            0xBB,
            0xEF,
            0xD4,
        };

        /// <summary>The kind of the 80 bytes that head the images, which AutoCAD fills with zeros.</summary>
        private const int HeaderKind = 1;

        /// <summary>The kind of a bitmap of Windows, without the 14 bytes of a file's header.</summary>
        private const int BitmapKind = 2;

        /// <summary>The kind of a PNG file.</summary>
        private const int PngKind = 6;

        private const int HeaderSize = 80;

        /// <summary>The bytes of the preview that stand at <paramref name="seeker"/> in the file; null where there are none or they do not fit.</summary>
        public static byte[] Block(byte[] file, long seeker)
        {
            if (
                seeker <= 0
                || seeker + Start.Length + 5 + End.Length > file.LongLength
                || !Matches(file, seeker, Start)
            )
            {
                return null;
            }

            long size = (uint)BitConverter.ToInt32(file, (int)seeker + Start.Length);
            long total = Start.Length + 4 + size + End.Length;

            if (size < 1 || seeker + total > file.LongLength || !Matches(file, seeker + total - End.Length, End))
            {
                return null;
            }

            var block = new byte[total];
            Array.Copy(file, seeker, block, 0, total);
            return block;
        }

        /// <summary>
        /// The picture of a preview <paramref name="block"/>: its bitmap, else its PNG; null where it holds none, as in the
        /// preview AutoCAD writes of a drawing it has drawn no picture of (a header of 80 zero bytes alone).
        /// </summary>
        public static byte[] Image(byte[] block)
        {
            if (block == null || block.Length < Start.Length + 5 + End.Length)
            {
                return null;
            }

            int count = block[Start.Length + 4];
            int list = Start.Length + 5;

            if (count < 1 || list + (count * 9) > block.Length - End.Length)
            {
                return null;
            }

            // The places are those in the file: the first image follows the list, which says where the section begins.
            long origin = (uint)BitConverter.ToInt32(block, list + 1) - (list + (count * 9));
            byte[] bitmap = null;
            byte[] png = null;

            for (int i = 0; i < count; i++)
            {
                int at = list + (i * 9);
                int kind = block[at];
                long from = (uint)BitConverter.ToInt32(block, at + 1) - origin;
                long size = (uint)BitConverter.ToInt32(block, at + 5);

                if (kind == HeaderKind || size <= 0 || from < list || from + size > block.Length - End.Length)
                {
                    continue;
                }

                var image = new byte[size];
                Array.Copy(block, from, image, 0, size);

                if (kind == BitmapKind)
                {
                    bitmap = image;
                }
                else if (kind == PngKind)
                {
                    png = image;
                }
            }

            return bitmap ?? png;
        }

        /// <summary>
        /// The preview of the picture <paramref name="image"/> (a bitmap of Windows without its file header, or a PNG file; null
        /// for none, which gives the empty preview the writer has always written: a size of 1 and no image), as AutoCAD 2021
        /// writes it, whose section's data begins at <paramref name="address"/> in the file.
        /// </summary>
        public static byte[] Build(byte[] image, long address)
        {
            var preview = new MemoryStream();
            preview.Write(Start, 0, Start.Length);

            if (image == null || image.Length == 0)
            {
                // No picture: a size of 1 and no image.
                preview.Write(BitConverter.GetBytes(1), 0, 4);
                preview.WriteByte(0);
                preview.Write(End, 0, End.Length);
                return preview.ToArray();
            }

            bool png = image.Length > 4 && image[0] == 0x89 && image[1] == 0x50 && image[2] == 0x4E;
            long data = address + Start.Length + 5 + (2 * 9);
            preview.Write(BitConverter.GetBytes(1 + (2 * 9) + HeaderSize + image.Length), 0, 4);
            preview.WriteByte(2);
            preview.WriteByte(HeaderKind);
            preview.Write(BitConverter.GetBytes((int)data), 0, 4);
            preview.Write(BitConverter.GetBytes(HeaderSize), 0, 4);
            preview.WriteByte(png ? (byte)PngKind : (byte)BitmapKind);
            preview.Write(BitConverter.GetBytes((int)(data + HeaderSize)), 0, 4);
            preview.Write(BitConverter.GetBytes(image.Length), 0, 4);
            preview.Write(new byte[HeaderSize], 0, HeaderSize);
            preview.Write(image, 0, image.Length);
            preview.Write(End, 0, End.Length);
            return preview.ToArray();
        }

        private static bool Matches(byte[] file, long at, byte[] sentinel)
        {
            for (int i = 0; i < sentinel.Length; i++)
            {
                if (file[at + i] != sentinel[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
