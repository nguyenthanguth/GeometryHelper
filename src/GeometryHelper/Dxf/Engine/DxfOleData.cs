using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The binary of an OLE2FRAME read, for <see cref="DxfOle2Frame"/>: AutoCAD's head of 128 bytes, then the compound file MFC's
    /// <c>COleClientItem::Serialize</c> writes, and what the streams of that file say of the object, as MS-OLEDS lays them out.
    /// </summary>
    /// <remarks>
    /// The head: the signature (two bytes, 0x80 0x55 in model space), the four corners upper left, upper right, lower right,
    /// lower left as twelve doubles (at 2), the natural width and height in hundredths of a millimetre (two u16 at 98), a u32
    /// (at 102), then MFC's 256, the item number, the aspect, the moniker flag (a u16), the drawing aspect and the length of
    /// the compound file (a u32 at 124); the compound file from 128.
    /// </remarks>
    internal static class DxfOleData
    {
        /// <summary>The bytes of the head: AutoCAD's 98, its natural size and u32, and MFC's 24.</summary>
        internal const int HeadSize = 128;

        /// <summary>The bytes of the head up to the end of its corners: the signature's 2 and the twelve doubles.</summary>
        internal const int CornersEnd = 98;

        private static readonly Guid StdOleLink = new Guid("00000300-0000-0000-C000-000000000046");
        private static readonly Guid FileMoniker = new Guid("00000303-0000-0000-C000-000000000046");
        private static readonly Guid ItemMoniker = new Guid("00000304-0000-0000-C000-000000000046");
        private static readonly Guid CompositeMoniker = new Guid("00000309-0000-0000-C000-000000000046");
        private static readonly Guid StaticMetafile = new Guid("00000315-0000-0000-C000-000000000046");
        private static readonly Guid StaticDib = new Guid("00000316-0000-0000-C000-000000000046");
        private static readonly Guid StaticEnhancedMetafile = new Guid("00000319-0000-0000-C000-000000000046");

        // A path of letters beyond ASCII is read a byte a letter: the code page of the machine that wrote it is not known.
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>
        /// The bytes of the binary groups (310) of a record, in their order, as <see cref="DxfBinary.Bytes"/> reads each line; no
        /// more than <paramref name="limit"/> where it is not -1, as many lines read as give them.
        /// </summary>
        internal static byte[] Binary(IReadOnlyList<DxfTypedValue> groups, int limit)
        {
            long letters = 0;

            foreach (DxfTypedValue group in groups)
            {
                letters += group.TypeCode == 310 ? group.Value?.Length ?? 0 : 0;
            }

            var bytes = new byte[limit >= 0 ? Math.Min(limit, letters / 2) : letters / 2];
            int at = 0;

            foreach (DxfTypedValue group in groups)
            {
                if (at >= bytes.Length)
                {
                    break;
                }

                if (group.TypeCode != 310)
                {
                    continue;
                }

                int high = -1;

                foreach (char c in group.Value ?? string.Empty)
                {
                    int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;

                    if (digit < 0)
                    {
                        continue;
                    }

                    if (high < 0)
                    {
                        high = digit;
                    }
                    else if (at < bytes.Length)
                    {
                        bytes[at++] = (byte)((high << 4) | digit);
                        high = -1;
                    }
                }
            }

            if (at < bytes.Length)
            {
                Array.Resize(ref bytes, at);
            }

            return bytes;
        }

        /// <summary>
        /// The lines of binary given, written anew with the bytes they hold from the first replaced by <paramref name="bytes"/>,
        /// each line keeping its count of bytes and the case of its letters, so that a line those bytes are not in comes back as
        /// it was; null where a line holds anything but pairs of hexadecimal digits, which could not be written back as it was,
        /// or the lines hold fewer bytes than given.
        /// </summary>
        internal static string[] Patched(IReadOnlyList<string> lines, byte[] bytes)
        {
            var written = new string[lines.Count];
            int at = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i] ?? string.Empty;

                if (line.Length % 2 != 0 || Digits(line) != line.Length)
                {
                    return null;
                }

                bool lower = false;

                foreach (char c in line)
                {
                    lower |= c >= 'a' && c <= 'f';
                }

                string digits = lower ? "0123456789abcdef" : "0123456789ABCDEF";
                var text = new StringBuilder(line.Length);

                for (int k = 0; k < line.Length; k += 2)
                {
                    if (at < bytes.Length)
                    {
                        text.Append(digits[bytes[at] >> 4]).Append(digits[bytes[at] & 15]);
                        at++;
                    }
                    else
                    {
                        text.Append(line, k, 2);
                    }
                }

                written[i] = text.ToString();
            }

            return at < bytes.Length ? null : written;
        }

        /// <summary>The hexadecimal digits of a line of binary, each half a byte, as <see cref="Binary"/> reads them.</summary>
        internal static int Digits(string line)
        {
            int digits = 0;

            foreach (char c in line ?? string.Empty)
            {
                digits += (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f') ? 1 : 0;
            }

            return digits;
        }

        /// <summary>
        /// The four corners of the head, upper left, upper right, lower right and lower left; false where the binary is too short
        /// to hold them or a coordinate is not a finite number.
        /// </summary>
        internal static bool TryCorners(byte[] binary, out GeoPoint3[] corners)
        {
            corners = null;

            if (binary.Length < CornersEnd)
            {
                return false;
            }

            var read = new GeoPoint3[4];

            for (int k = 0; k < 4; k++)
            {
                double x = BitConverter.ToDouble(binary, 2 + 24 * k);
                double y = BitConverter.ToDouble(binary, 10 + 24 * k);
                double z = BitConverter.ToDouble(binary, 18 + 24 * k);

                if (!Guard.IsFinite(x) || !Guard.IsFinite(y) || !Guard.IsFinite(z))
                {
                    return false;
                }

                read[k] = new GeoPoint3(x, y, z);
            }

            corners = read;
            return true;
        }

        /// <summary>
        /// The compound file after the head, as many bytes as the head counts (its u32 at 124), or those there are where it counts
        /// more; null where the binary holds no head.
        /// </summary>
        internal static byte[] CompoundBytes(byte[] binary)
        {
            if (binary.Length < HeadSize)
            {
                return null;
            }

            long counted = DxfCompoundFile.U32(binary, 124);
            var bytes = new byte[Math.Min(counted, binary.Length - HeadSize)];
            Array.Copy(binary, HeadSize, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>The compound file of the binary, read; null, with the reason, where there is none or it is damaged.</summary>
        internal static DxfCompoundFile Compound(byte[] binary, out string why)
        {
            if (binary.Length < HeadSize)
            {
                why = "the binary holds " + binary.Length + " bytes, fewer than its head of 128";
                return null;
            }

            long counted = DxfCompoundFile.U32(binary, 124);
            return DxfCompoundFile.Read(binary, HeadSize, (int)Math.Min(counted, binary.Length - HeadSize), out why);
        }

        /// <summary>
        /// The kind of the object, as MFC's <c>COleClientItem::GetType</c> finds it: a link where its storage is of the class of a
        /// link (<c>CLSID_StdOleLink</c>) or its <c>\1Ole</c> stream says it is linked; static where it is of a class of a static
        /// picture (DIB, metafile, enhanced metafile); embedded otherwise.
        /// </summary>
        internal static DxfOle2FrameItemType TypeOf(DxfCompoundFile file)
        {
            byte[] ole = file.Stream("\u0001Ole", out _);

            if (file.RootClassId == StdOleLink || (ole != null && ole.Length >= 8 && (DxfCompoundFile.U32(ole, 4) & 1) != 0))
            {
                return DxfOle2FrameItemType.Link;
            }

            return PictureOf(file.RootClassId) != DxfOlePictureFormat.None ? DxfOle2FrameItemType.Static : DxfOle2FrameItemType.Embedded;
        }

        /// <summary>
        /// The user type of the object: of a static picture, the name of its class, as AutoCAD 2021 asks Windows' registry for
        /// it; of another, as its <c>\1CompObj</c> stream (MS-OLEDS 2.3.8) gives it, its Unicode user type where it has one, else
        /// its ANSI one where it is of letters of ASCII; null where the stream gives neither, or gives an empty one.
        /// </summary>
        internal static string UserTypeOf(DxfCompoundFile file)
        {
            // A static picture is named by its class, as Windows' registry names the three.
            switch (PictureOf(file.RootClassId))
            {
                case DxfOlePictureFormat.Dib:
                    return "Picture (Device Independent Bitmap)";
                case DxfOlePictureFormat.Metafile:
                    return "Picture (Metafile)";
                case DxfOlePictureFormat.EnhancedMetafile:
                    return "Picture (Enhanced Metafile)";
            }

            byte[] stream = file.Stream("\u0001CompObj", out _);

            if (stream == null || stream.Length < 32)
            {
                return null;
            }

            int at = 28;
            byte[] ansi = AnsiString(stream, ref at);
            string named = Ascii(ansi);
            named = string.IsNullOrEmpty(named) ? null : named;

            // The clipboard format, then the ProgID, whose length of 0 or over 0x28 says the Unicode strings after it are to be
            // ignored, as MS-OLEDS 2.3.8 says; then the marker of the Unicode strings.
            uint progId = ansi != null && SkipClipboardFormat(stream, ref at, 1) && at + 4 <= stream.Length ? DxfCompoundFile.U32(stream, at) : 0;

            if (progId == 0 || progId > 0x28 || AnsiString(stream, ref at) == null)
            {
                return named;
            }

            if (at + 4 <= stream.Length && DxfCompoundFile.U32(stream, at) == 0x71B239F4)
            {
                at += 4;
                string unicode = UnicodeString(stream, ref at);

                if (!string.IsNullOrEmpty(unicode))
                {
                    return unicode;
                }
            }

            return named;
        }

        /// <summary>
        /// The source of a linked object, from its <c>\1Ole</c> stream (MS-OLEDS 2.3.3): the display name of its absolute moniker,
        /// or of its relative one where it has no absolute one that is read (a file moniker's path, an item moniker's delimiter
        /// and item, a composite's in turn), and the path of its first file moniker. False where the object is not linked, or
        /// its monikers are not of the kinds read.
        /// </summary>
        internal static bool TryLink(DxfCompoundFile file, out string name, out string path)
        {
            name = null;
            path = null;
            byte[] ole = file.Stream("\u0001Ole", out _);

            if (ole == null || ole.Length < 20)
            {
                return false;
            }

            int at = 16;
            uint reserved = DxfCompoundFile.U32(ole, at);
            at += 4;

            // Each moniker's size counts the moniker and the 4 bytes of the size itself, MS-OLEDS 2.3.3 says; 0 where there is none.
            if (reserved != 0)
            {
                if (reserved < 4 || reserved - 4 > ole.Length - at)
                {
                    return false;
                }

                at += (int)reserved - 4;
            }

            string relativeName = null;
            string relativePath = null;

            for (int k = 0; k < 2 && at + 4 <= ole.Length; k++)
            {
                uint size = DxfCompoundFile.U32(ole, at);
                at += 4;

                if (size == 0)
                {
                    continue;
                }

                if (size < 4 || size - 4 > ole.Length - at)
                {
                    break;
                }

                int start = at;
                int end = start + (int)size - 4;
                string shown = null;
                string file0 = null;
                bool read = Moniker(ole, ref at, end, ref shown, ref file0, 0);

                if (k == 0)
                {
                    relativeName = read ? shown : null;
                    relativePath = read ? file0 : null;
                }
                else if (read)
                {
                    name = shown;
                    path = file0 ?? string.Empty;
                    return true;
                }

                // The next field begins where the size says this moniker ends, read or not.
                at = end;
            }

            name = relativeName;
            path = relativeName == null ? null : relativePath ?? string.Empty;
            return relativeName != null;
        }

        /// <summary>
        /// The picture of a static object as the bytes of its file: the <c>CONTENTS</c> stream of a DIB, a .bmp with its file
        /// header; of a metafile, a placeable .wmf; of an enhanced metafile, the .emf after the size and copy of its header that
        /// precede it. Null, with the reason, where the object is not static or its stream is not of its form.
        /// </summary>
        internal static byte[] Picture(DxfCompoundFile file, out DxfOlePictureFormat format, out string why)
        {
            why = null;
            format = PictureOf(file.RootClassId);

            if (format == DxfOlePictureFormat.None)
            {
                return null;
            }

            byte[] contents = file.Stream("CONTENTS", out why);
            DxfOlePictureFormat kind = format;
            format = DxfOlePictureFormat.None;

            if (contents == null)
            {
                why = why ?? "it has no stream CONTENTS";
                return null;
            }

            switch (kind)
            {
                case DxfOlePictureFormat.Dib:
                    if (contents.Length < 54 || contents[0] != 0x42 || contents[1] != 0x4D)
                    {
                        why = "its CONTENTS is not a .bmp";
                        return null;
                    }

                    format = kind;
                    return contents;

                case DxfOlePictureFormat.Metafile:
                    if (contents.Length < 40 || DxfCompoundFile.U32(contents, 0) != 0x9AC6CDD7)
                    {
                        why = "its CONTENTS is not a placeable metafile";
                        return null;
                    }

                    format = kind;
                    return contents;

                default:
                    long header = contents.Length >= 4 ? DxfCompoundFile.U32(contents, 0) : -1;
                    long from = 4 + header;
                    long size = header >= 0 && from + 52 <= contents.Length ? DxfCompoundFile.U32(contents, (int)from + 48) : -1;

                    if (size < 108 || from + size > contents.Length || DxfCompoundFile.U32(contents, (int)from) != 1 || DxfCompoundFile.U32(contents, (int)from + 40) != 0x464D4520)
                    {
                        why = "its CONTENTS does not hold an enhanced metafile after the copy of its header";
                        return null;
                    }

                    var emf = new byte[size];
                    Array.Copy(contents, from, emf, 0, size);
                    format = kind;
                    return emf;
            }
        }

        /// <summary>The kind of picture of a static object by the class of its storage; none for another class.</summary>
        private static DxfOlePictureFormat PictureOf(Guid classId)
            => classId == StaticDib ? DxfOlePictureFormat.Dib : classId == StaticMetafile ? DxfOlePictureFormat.Metafile : classId == StaticEnhancedMetafile ? DxfOlePictureFormat.EnhancedMetafile : DxfOlePictureFormat.None;

        /// <summary>
        /// Reads a moniker (MS-OLEDS's MONIKERSTREAM: its class, then its data) up to an end: a file moniker's path, an item
        /// moniker's delimiter and item, a composite's monikers in turn, joined into its display name; the path of the first file
        /// moniker. False for another class, or data running past the end.
        /// </summary>
        private static bool Moniker(byte[] data, ref int at, int end, ref string shown, ref string filePath, int depth)
        {
            if (at + 16 > end || depth > 8)
            {
                return false;
            }

            var bytes = new byte[16];
            Array.Copy(data, at, bytes, 0, 16);
            var classId = new Guid(bytes);
            at += 16;

            if (classId == FileMoniker)
            {
                // cAnti, the ANSI path, endServer, version 0xDEAD, 20 reserved, then the Unicode path where its size is not 0.
                if (at + 6 > end)
                {
                    return false;
                }

                int anti = DxfCompoundFile.U16(data, at);
                uint length = DxfCompoundFile.U32(data, at + 2);
                at += 6;

                if (length > end - at)
                {
                    return false;
                }

                byte[] ansi = Until(data, at, (int)length);
                string text = Ascii(ansi) ?? Latin1.GetString(ansi);
                at += (int)length;

                if (at + 28 > end)
                {
                    return false;
                }

                at += 24;
                uint unicodeSize = DxfCompoundFile.U32(data, at);
                at += 4;

                if (unicodeSize != 0)
                {
                    if (unicodeSize < 6 || unicodeSize > end - at)
                    {
                        return false;
                    }

                    uint pathBytes = DxfCompoundFile.U32(data, at);

                    if (pathBytes <= unicodeSize - 6)
                    {
                        text = Encoding.Unicode.GetString(data, at + 6, (int)pathBytes & ~1);
                    }

                    at += (int)unicodeSize;
                }

                var up = new StringBuilder();

                for (int i = 0; i < anti; i++)
                {
                    up.Append("..\\");
                }

                string path = up + text;
                shown = (shown ?? string.Empty) + path;
                filePath = filePath ?? path;
                return true;
            }

            if (classId == ItemMoniker)
            {
                string delimiter = ItemPart(data, ref at, end);
                string item = delimiter == null ? null : ItemPart(data, ref at, end);

                if (item == null)
                {
                    return false;
                }

                shown = (shown ?? string.Empty) + delimiter + item;
                return true;
            }

            if (classId == CompositeMoniker)
            {
                if (at + 4 > end)
                {
                    return false;
                }

                uint count = DxfCompoundFile.U32(data, at);
                at += 4;

                for (uint i = 0; i < count; i++)
                {
                    if (!Moniker(data, ref at, end, ref shown, ref filePath, depth + 1))
                    {
                        return false;
                    }
                }

                return shown != null;
            }

            return false;
        }

        /// <summary>
        /// A delimiter or an item of an item moniker: its size, its ANSI letters closed by a nought, then, within the size, the
        /// same in Unicode, which is taken where it is there; null where it runs past the end.
        /// </summary>
        private static string ItemPart(byte[] data, ref int at, int end)
        {
            if (at + 4 > end)
            {
                return null;
            }

            uint size = DxfCompoundFile.U32(data, at);
            at += 4;

            if (size > end - at)
            {
                return null;
            }

            byte[] ansi = Until(data, at, (int)size);
            int unicode = (int)size - ansi.Length - 1;
            string text = unicode >= 2 ? Encoding.Unicode.GetString(data, at + ansi.Length + 1, unicode & ~1) : Ascii(ansi) ?? Latin1.GetString(ansi);
            at += (int)size;
            return text;
        }

        /// <summary>A LengthPrefixedAnsiString: its length with the closing nought, then its letters; its bytes without the nought, null where it runs past the end.</summary>
        private static byte[] AnsiString(byte[] data, ref int at)
        {
            if (at + 4 > data.Length)
            {
                return null;
            }

            uint length = DxfCompoundFile.U32(data, at);
            at += 4;

            if (length > data.Length - at)
            {
                return null;
            }

            byte[] text = Until(data, at, (int)length);
            at += (int)length;
            return text;
        }

        /// <summary>A LengthPrefixedUnicodeString: its length in letters with the closing nought, then its letters; null where it runs past the end.</summary>
        private static string UnicodeString(byte[] data, ref int at)
        {
            if (at + 4 > data.Length)
            {
                return null;
            }

            uint length = DxfCompoundFile.U32(data, at);
            at += 4;

            if (length > (data.Length - at) / 2)
            {
                return null;
            }

            string text = Encoding.Unicode.GetString(data, at, (int)length * 2);
            at += (int)length * 2;
            return text.TrimEnd('\0');
        }

        /// <summary>Passes a ClipboardFormatOrAnsiString (or, of letters of <paramref name="size"/> bytes, its Unicode twin): none, a standard format, or a name.</summary>
        private static bool SkipClipboardFormat(byte[] data, ref int at, int size)
        {
            if (at + 4 > data.Length)
            {
                return false;
            }

            uint marker = DxfCompoundFile.U32(data, at);
            at += 4;

            if (marker == 0xFFFFFFFF || marker == 0xFFFFFFFE)
            {
                at += 4;
                return at <= data.Length;
            }

            if ((long)marker * size > data.Length - at)
            {
                return false;
            }

            at += (int)marker * size;
            return true;
        }

        /// <summary>The bytes of a run up to its first nought or its end.</summary>
        private static byte[] Until(byte[] data, int at, int count)
        {
            int n = 0;

            while (n < count && data[at + n] != 0)
            {
                n++;
            }

            var bytes = new byte[n];
            Array.Copy(data, at, bytes, 0, n);
            return bytes;
        }

        /// <summary>The letters of bytes all of ASCII; null where one is not, or there are none to read.</summary>
        private static string Ascii(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            foreach (byte b in bytes)
            {
                if (b >= 0x80)
                {
                    return null;
                }
            }

            return Encoding.ASCII.GetString(bytes);
        }
    }
}
