using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// OLE objects for the tests of <c>DxfOle2Frame</c>, built in code as Windows' OLE and AutoCAD 2021 write them, by a writer of
    /// compound files of the tests' own (MS-CFB), apart from the library's reader: the streams of a static bitmap, metafile and
    /// enhanced metafile (MS-OLEDS), of a file packed and of a link, the head AutoCAD writes before them, and OLE2FRAME records
    /// appended to the drawing of R2018 of <see cref="DxfFileEzdxfDrawings"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>D10, a picture of 3 by 2 pixels (a .bmp of 78 bytes) pasted with its lower left corner at (10, 20), 30 by 20.</item>
    /// <item>D11, a metafile from (100, 0) to (130, 20); D12, an enhanced metafile from (200, 0) to (230, 20).</item>
    /// <item>D13, a file packed (OLE Package) from (300, 0) to (340, 30).</item>
    /// <item>D14, a link to the range Sheet1!R1C1:R4C3 of C:\Drawings\cost.xlsx, from (400, 0) to (460, 40).</item>
    /// <item>D15, D10's object with the signature of its compound file broken, from (0, 100) to (30, 120), its record saying
    /// another kind and name (71 of 2, 3 of Something Else).</item>
    /// <item>D16, D10's object whose head stands 100 along x and 50 along y from its groups 10 and 11.</item>
    /// <item>D17, D10's object in paper space, its signature 01 55, from (5, 5) to (35, 25).</item>
    /// <item>D18, a binary of 50 bytes, too short for the corners: groups 10 (0, 220) and 11 (40, 200), 71 of 2 and 3 of
    /// Something Else.</item>
    /// <item>D19, D10's object turned 30 degrees about its lower left corner (0, 300), its extended data turned, unlocked and of
    /// fixed quality (70 0, 71 0, 40 0.5236, 72 1) and plotted monochrome (73 0).</item>
    /// <item>D1A, D10's object with no extended data, from (50, 300) to (80, 320).</item>
    /// </list>
    /// </remarks>
    internal static class DxfOle2FrameDrawings
    {
        internal static readonly Guid StaticDib = new Guid("00000316-0000-0000-C000-000000000046");
        internal static readonly Guid StaticMetafile = new Guid("00000315-0000-0000-C000-000000000046");
        internal static readonly Guid StaticEnhancedMetafile = new Guid("00000319-0000-0000-C000-000000000046");
        internal static readonly Guid Package = new Guid("0003000C-0000-0000-C000-000000000046");
        internal static readonly Guid StdOleLink = new Guid("00000300-0000-0000-C000-000000000046");

        private static readonly Guid FileMonikerClass = new Guid("00000303-0000-0000-C000-000000000046");
        private static readonly Guid ItemMonikerClass = new Guid("00000304-0000-0000-C000-000000000046");
        private static readonly Guid AntiMonikerClass = new Guid("00000305-0000-0000-C000-000000000046");
        private static readonly Guid CompositeMonikerClass = new Guid("00000309-0000-0000-C000-000000000046");
        private static readonly Guid ExcelSheet12 = new Guid("00020830-0000-0000-C000-000000000046");

        /// <summary>The drawing of R2018 with the records appended.</summary>
        internal static string R2018 => With(DxfFileEzdxfDrawings.R2018, Records());

        /// <summary>The drawing of R2018 with records appended.</summary>
        internal static string With(string drawing, string records)
        {
            int entities = drawing.IndexOf("\nENTITIES\n", StringComparison.Ordinal);
            int end = drawing.IndexOf("  0\nENDSEC\n", entities, StringComparison.Ordinal);
            return drawing.Substring(0, end) + records + drawing.Substring(end);
        }

        /// <summary>The records of the remarks, in their order.</summary>
        internal static string Records()
        {
            var b = new StringBuilder();
            byte[] dib = DibCompound();
            Frame(
                b,
                "D10",
                false,
                "Picture (Device Independent Bitmap)",
                3,
                Head(Square(10, 20, 30, 20), 0x80, dib),
                true
            );
            Frame(
                b,
                "D11",
                false,
                "Picture (Metafile)",
                3,
                Head(Square(100, 0, 30, 20), 0x80, MetafileCompound()),
                true
            );
            Frame(
                b,
                "D12",
                false,
                "Picture (Enhanced Metafile)",
                3,
                Head(Square(200, 0, 30, 20), 0x80, EnhancedMetafileCompound()),
                true
            );
            Frame(b, "D13", false, "OLE Package", 2, Head(Square(300, 0, 40, 30), 0x80, PackageCompound()), true);
            Frame(
                b,
                "D14",
                false,
                "Microsoft Excel Worksheet",
                1,
                Head(Square(400, 0, 60, 40), 0x80, LinkCompound()),
                true
            );

            byte[] broken = (byte[])dib.Clone();
            broken[0] = 0;
            Frame(b, "D15", false, "Something Else", 2, Head(Square(0, 100, 30, 20), 0x80, broken), true);

            GeoPoint3[] moved = Square(10, 20, 30, 20).Select(p => new GeoPoint3(p.X + 100, p.Y + 50, p.Z)).ToArray();
            Frame(
                b,
                "D16",
                false,
                "Picture (Device Independent Bitmap)",
                3,
                Head(moved, 0x80, dib),
                true,
                Square(10, 20, 30, 20)
            );
            Frame(
                b,
                "D17",
                true,
                "Picture (Device Independent Bitmap)",
                3,
                Head(Square(5, 5, 30, 20), 0x01, dib),
                true
            );
            Frame(
                b,
                "D18",
                false,
                "Something Else",
                2,
                Enumerable.Range(0, 50).Select(i => (byte)i).ToArray(),
                true,
                Square(0, 200, 40, 20)
            );

            double turn = Math.PI / 6;
            GeoPoint3[] turned = Square(0, 300, 30, 20)
                .Select(p => new GeoPoint3(
                    Math.Cos(turn) * p.X - Math.Sin(turn) * (p.Y - 300),
                    300 + Math.Sin(turn) * p.X + Math.Cos(turn) * (p.Y - 300),
                    0
                ))
                .ToArray();
            Frame(
                b,
                "D19",
                false,
                "Picture (Device Independent Bitmap)",
                3,
                Head(turned, 0x80, dib),
                true,
                null,
                0,
                "0",
                "0",
                "0.5235987755982988",
                "1"
            );
            Frame(
                b,
                "D1A",
                false,
                "Picture (Device Independent Bitmap)",
                3,
                Head(Square(50, 300, 30, 20), 0x80, dib),
                false
            );
            return b.ToString();
        }

        /// <summary>The corners upper left, upper right, lower right and lower left of a frame along x and y from its lower left corner.</summary>
        internal static GeoPoint3[] Square(double x, double y, double width, double height) =>
            new[]
            {
                new GeoPoint3(x, y + height, 0),
                new GeoPoint3(x + width, y + height, 0),
                new GeoPoint3(x + width, y, 0),
                new GeoPoint3(x, y, 0),
            };

        /// <summary>A .bmp of 3 by 2 pixels of 24 bits, 3 780 pixels a metre (96 dots an inch): 78 bytes.</summary>
        internal static byte[] Bitmap()
        {
            var b = new List<byte>();
            b.AddRange(new byte[] { 0x42, 0x4D });
            b.AddRange(U32(78));
            b.AddRange(U32(0));
            b.AddRange(U32(54));
            b.AddRange(U32(40));
            b.AddRange(U32(3));
            b.AddRange(U32(2));
            b.AddRange(U16(1));
            b.AddRange(U16(24));
            b.AddRange(U32(0));
            b.AddRange(U32(24));
            b.AddRange(U32(3780));
            b.AddRange(U32(3780));
            b.AddRange(U32(0));
            b.AddRange(U32(0));

            // Two rows of three pixels, blue, green, red and white, black, grey, each row padded to 12 bytes.
            b.AddRange(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0 });
            b.AddRange(new byte[] { 255, 255, 255, 0, 0, 0, 128, 128, 128, 0, 0, 0 });
            return b.ToArray();
        }

        /// <summary>A placeable metafile of 300 by 200 at 1 440 a inch, its metafile only its header and end: 46 bytes.</summary>
        internal static byte[] PlaceableMetafile()
        {
            var b = new List<byte>();
            b.AddRange(U32(0x9AC6CDD7));
            b.AddRange(U16(0));
            b.AddRange(U16(0));
            b.AddRange(U16(0));
            b.AddRange(U16(300));
            b.AddRange(U16(200));
            b.AddRange(U16(1440));
            b.AddRange(U32(0));
            ushort sum = 0;

            for (int i = 0; i < 20; i += 2)
            {
                sum ^= (ushort)(b[i] | (b[i + 1] << 8));
            }

            b.AddRange(U16(sum));

            // The metafile: its header of 9 words, 12 words in all, its largest record of 3; then the end record.
            b.AddRange(U16(1));
            b.AddRange(U16(9));
            b.AddRange(U16(0x0300));
            b.AddRange(U32(12));
            b.AddRange(U16(0));
            b.AddRange(U32(3));
            b.AddRange(U16(0));
            b.AddRange(U32(3));
            b.AddRange(U16(0));
            return b.ToArray();
        }

        /// <summary>An enhanced metafile of its header record and end record: 128 bytes.</summary>
        internal static byte[] EnhancedMetafile()
        {
            var b = new List<byte>();
            b.AddRange(U32(1));
            b.AddRange(U32(108));
            b.AddRange(I32s(0, 0, 299, 199));
            b.AddRange(I32s(0, 0, 7937, 5291));
            b.AddRange(U32(0x464D4520));
            b.AddRange(U32(0x10000));
            b.AddRange(U32(128));
            b.AddRange(U32(2));
            b.AddRange(U16(1));
            b.AddRange(U16(0));
            b.AddRange(U32(0));
            b.AddRange(U32(0));
            b.AddRange(U32(0));
            b.AddRange(I32s(1920, 1080, 508, 286));
            b.AddRange(U32(0));
            b.AddRange(U32(0));
            b.AddRange(U32(0));
            b.AddRange(I32s(508000, 285750));

            // The end record.
            b.AddRange(U32(14));
            b.AddRange(U32(20));
            b.AddRange(U32(0));
            b.AddRange(U32(16));
            b.AddRange(U32(20));
            return b.ToArray();
        }

        /// <summary>The compound file of a static bitmap, as ole32's <c>OleCreateStaticFromData</c> and <c>OleSave</c> write it.</summary>
        internal static byte[] DibCompound(int shift = 9) =>
            Compound(
                StaticDib,
                shift,
                Stream("\u0001Ole", OleStream()),
                Stream("\u0001CompObj", CompObj("Picture (Device Independent Bitmap)", 8, "StaticDib")),
                Stream("CONTENTS", Bitmap())
            );

        /// <summary>The compound file of a static metafile.</summary>
        internal static byte[] MetafileCompound() =>
            Compound(
                StaticMetafile,
                9,
                Stream("\u0001Ole", OleStream()),
                Stream("\u0001CompObj", CompObj("Picture (Metafile)", 3, "StaticMetafile")),
                Stream("CONTENTS", PlaceableMetafile())
            );

        /// <summary>The compound file of a static enhanced metafile: its CONTENTS the size of its header, a copy of it, then the metafile.</summary>
        internal static byte[] EnhancedMetafileCompound()
        {
            byte[] emf = EnhancedMetafile();
            byte[] contents = U32(108).Concat(emf.Take(108)).Concat(emf).ToArray();
            return Compound(
                StaticEnhancedMetafile,
                9,
                Stream("\u0001Ole", OleStream()),
                Stream("\u0001CompObj", CompObj("Picture (Enhanced Metafile)", 14, null)),
                Stream("CONTENTS", contents)
            );
        }

        /// <summary>The compound file of a file packed, as <c>OleCreateFromFile</c> writes one for a type of no application of OLE.</summary>
        internal static byte[] PackageCompound(string userType = "OLE Package")
        {
            var native = new List<byte>();
            native.AddRange(U16(2));
            native.AddRange(Ansi("hello.txt"));
            native.AddRange(Ansi("C:\\Temp\\hello.txt"));
            native.AddRange(U16(0));
            native.AddRange(U16(3));
            native.AddRange(U32(18));
            native.AddRange(Ansi("C:\\Temp\\hello.txt"));
            native.AddRange(U32(5));
            native.AddRange(Encoding.ASCII.GetBytes("hello"));
            byte[] stream = U32((uint)native.Count).Concat(native).ToArray();
            return Compound(
                Package,
                9,
                Stream("\u0001CompObj", CompObj(userType, 0, "Package")),
                Stream("\u0001Ole10Native", stream)
            );
        }

        /// <summary>
        /// The compound file of a link to a range of a workbook: of the class of a link, its <c>\1Ole</c> stream linked, of a
        /// relative moniker ..\cost.xlsx and an absolute one C:\Drawings\cost.xlsx, each with the item Sheet1!R1C1:R4C3, each
        /// moniker's size counting its 4 bytes, as MS-OLEDS 2.3.3 says. Where asked, the relative moniker begins with an anti
        /// moniker, of a kind the library does not read.
        /// </summary>
        internal static byte[] LinkCompound(bool antiRelative = false)
        {
            byte[] item = Moniker(ItemMonikerClass, ItemMoniker("!", "Sheet1!R1C1:R4C3"));
            byte[] file = Moniker(FileMonikerClass, FileMoniker(1, "cost.xlsx", false));
            byte[] relative = antiRelative
                ? Moniker(
                    CompositeMonikerClass,
                    U32(3).Concat(Moniker(AntiMonikerClass, U32(1))).Concat(file).Concat(item).ToArray()
                )
                : Moniker(CompositeMonikerClass, U32(2).Concat(file).Concat(item).ToArray());
            byte[] absolute = Moniker(
                CompositeMonikerClass,
                U32(2)
                    .Concat(Moniker(FileMonikerClass, FileMoniker(0, "C:\\Drawings\\cost.xlsx", true)))
                    .Concat(item)
                    .ToArray()
            );
            var ole = new List<byte>();
            ole.AddRange(U32(0x02000001));
            ole.AddRange(U32(1));
            ole.AddRange(U32(1));
            ole.AddRange(U32(0));
            ole.AddRange(U32(0));
            ole.AddRange(U32((uint)relative.Length + 4));
            ole.AddRange(relative);
            ole.AddRange(U32((uint)absolute.Length + 4));
            ole.AddRange(absolute);
            ole.AddRange(U32(0xFFFFFFFF));
            ole.AddRange(ExcelSheet12.ToByteArray());
            ole.AddRange(U32(0));
            ole.AddRange(U32(0));
            ole.AddRange(new byte[24]);
            return Compound(
                StdOleLink,
                9,
                Stream("\u0001Ole", ole.ToArray()),
                Stream("\u0001CompObj", CompObj("Microsoft Excel Worksheet", 0, "Excel.Sheet.12"))
            );
        }

        /// <summary>The <c>\1Ole</c> stream of an object embedded or static (MS-OLEDS 2.3.3): 20 bytes, no moniker.</summary>
        internal static byte[] OleStream() => U32(0x02000001).Concat(new byte[16]).ToArray();

        /// <summary>
        /// The <c>\1CompObj</c> stream (MS-OLEDS 2.3.8): its header, user type, clipboard format (0 for none), ProgID (null for
        /// one of length 0), then its Unicode user type, empty unless given, and empty Unicode clipboard format and ProgID.
        /// </summary>
        internal static byte[] CompObj(
            string userType,
            uint clipboardFormat,
            string progId,
            string unicodeUserType = null
        )
        {
            var b = new List<byte>();
            b.AddRange(U32(0xFFFE0001));
            b.AddRange(U32(0x00000A03));
            b.AddRange(U32(0xFFFFFFFF));
            b.AddRange(new byte[16]);
            b.AddRange(Prefixed(userType));

            if (clipboardFormat == 0)
            {
                b.AddRange(U32(0));
            }
            else
            {
                b.AddRange(U32(0xFFFFFFFF));
                b.AddRange(U32(clipboardFormat));
            }

            b.AddRange(progId == null ? U32(0) : Prefixed(progId));
            b.AddRange(U32(0x71B239F4));

            if (unicodeUserType == null)
            {
                b.AddRange(U32(0));
            }
            else
            {
                b.AddRange(U32((uint)unicodeUserType.Length + 1));
                b.AddRange(Encoding.Unicode.GetBytes(unicodeUserType + "\0"));
            }

            b.AddRange(new byte[8]);
            return b.ToArray();
        }

        /// <summary>
        /// A compound file of version 3 (sectors of 512 bytes, shift 9) or 4 (4 096, shift 12) whose root storage, of a class,
        /// holds the streams given: those under 4 096 bytes in its mini stream. Laid out as its FAT sectors, its DIFAT sectors
        /// where it has more FAT sectors than the header's 109, its directory, mini FAT and mini stream, then each stream of its
        /// own sectors; its directory a tree of the streams, balanced and red-black, by the order MS-CFB compares names in.
        /// </summary>
        internal static byte[] Compound(Guid root, int shift, params KeyValuePair<string, byte[]>[] streams)
        {
            const uint Free = 0xFFFFFFFF;
            const uint End = 0xFFFFFFFE;
            int size = 1 << shift;
            int per = size / 4;
            int n = streams.Length;

            var mini = new List<byte>();
            var miniFat = new List<uint>();
            var starts = new uint[n];
            var big = new List<int>();

            for (int i = 0; i < n; i++)
            {
                byte[] data = streams[i].Value;

                if (data.Length >= 4096)
                {
                    big.Add(i);
                    continue;
                }

                int count = (data.Length + 63) / 64;
                starts[i] = count == 0 ? End : (uint)miniFat.Count;

                for (int k = 0; k < count; k++)
                {
                    miniFat.Add(k < count - 1 ? (uint)(miniFat.Count + 1) : End);
                }

                mini.AddRange(data);
                mini.AddRange(new byte[count * 64 - data.Length]);
            }

            int directorySectors = ((n + 1) * 128 + size - 1) / size;
            int miniFatSectors = (miniFat.Count * 4 + size - 1) / size;
            int miniSectors = (mini.Count + size - 1) / size;
            var bigSectors = big.ConvertAll(i => (streams[i].Value.Length + size - 1) / size);
            int content = directorySectors + miniFatSectors + miniSectors + bigSectors.Sum();
            int fatCount = 1;
            int difatCount = 0;

            while (true)
            {
                int total = content + fatCount + difatCount;
                int fats = (total + per - 1) / per;
                int difats = fats > 109 ? (fats - 109 + per - 2) / (per - 1) : 0;

                if (fats == fatCount && difats == difatCount)
                {
                    break;
                }

                fatCount = fats;
                difatCount = difats;
            }

            int sectors = content + fatCount + difatCount;
            var fat = Enumerable.Repeat(Free, fatCount * per).ToArray();
            int next = 0;

            for (int i = 0; i < fatCount; i++)
            {
                fat[next++] = 0xFFFFFFFD;
            }

            int difatStart = next;

            for (int i = 0; i < difatCount; i++)
            {
                fat[next++] = 0xFFFFFFFC;
            }

            int Chain(int count)
            {
                int first = next;

                for (int k = 0; k < count; k++, next++)
                {
                    fat[next] = k < count - 1 ? (uint)(next + 1) : End;
                }

                return count == 0 ? unchecked((int)End) : first;
            }

            int directoryStart = Chain(directorySectors);
            int miniFatStart = Chain(miniFatSectors);
            int miniStart = Chain(miniSectors);

            for (int k = 0; k < big.Count; k++)
            {
                starts[big[k]] = (uint)Chain(bigSectors[k]);
            }

            var file = new byte[(sectors + 1) * size];

            // The header, of 512 bytes, its sector of 4 096 in version 4 zero after them.
            new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(file, 0);
            Put16(file, 0x18, 0x3E);
            Put16(file, 0x1A, shift == 9 ? 3 : 4);
            Put16(file, 0x1C, 0xFFFE);
            Put16(file, 0x1E, shift);
            Put16(file, 0x20, 6);
            Put32(file, 0x28, shift == 9 ? 0 : (uint)directorySectors);
            Put32(file, 0x2C, (uint)fatCount);
            Put32(file, 0x30, (uint)directoryStart);
            Put32(file, 0x38, 4096);
            Put32(file, 0x3C, miniFatSectors == 0 ? End : (uint)miniFatStart);
            Put32(file, 0x40, (uint)miniFatSectors);
            Put32(file, 0x44, difatCount == 0 ? End : (uint)difatStart);
            Put32(file, 0x48, (uint)difatCount);

            for (int i = 0; i < 109; i++)
            {
                Put32(file, 0x4C + 4 * i, i < fatCount ? (uint)i : Free);
            }

            // The DIFAT sectors: the FAT sectors past the 109th, then the next DIFAT sector.
            for (int d = 0; d < difatCount; d++)
            {
                int at = (difatStart + d + 1) * size;

                for (int i = 0; i < per - 1; i++)
                {
                    int listed = 109 + d * (per - 1) + i;
                    Put32(file, at + 4 * i, listed < fatCount ? (uint)listed : Free);
                }

                Put32(file, at + 4 * (per - 1), d < difatCount - 1 ? (uint)(difatStart + d + 1) : End);
            }

            for (int i = 0; i < fat.Length; i++)
            {
                Put32(file, (i / per + 1) * size + 4 * (i % per), fat[i]);
            }

            // The directory: the root, then the streams in their order, a tree of them under the root.
            var entries = new byte[directorySectors * size];
            int[] left = Enumerable.Repeat(-1, n).ToArray();
            int[] right = Enumerable.Repeat(-1, n).ToArray();
            var red = new bool[n];
            List<int> sorted = Enumerable
                .Range(0, n)
                .OrderBy(i => streams[i].Key.Length)
                .ThenBy(i => streams[i].Key.ToUpperInvariant(), StringComparer.Ordinal)
                .ToList();
            int depth = 0;

            while ((1 << (depth + 1)) - 1 < n)
            {
                depth++;
            }

            bool perfect = (1 << (depth + 1)) - 1 == n;

            int Build(int from, int to, int level)
            {
                if (from > to)
                {
                    return -1;
                }

                int middle = (from + to + 1) / 2;
                int node = sorted[middle];
                left[node] = Build(from, middle - 1, level + 1);
                right[node] = Build(middle + 1, to, level + 1);
                red[node] = !perfect && level == depth;
                return node;
            }

            int top = Build(0, n - 1, 0);
            Entry(
                entries,
                0,
                "Root Entry",
                5,
                false,
                -1,
                -1,
                top,
                root,
                mini.Count == 0 ? End : (uint)miniStart,
                (ulong)mini.Count
            );

            for (int i = 0; i < n; i++)
            {
                Entry(
                    entries,
                    i + 1,
                    streams[i].Key,
                    2,
                    red[i],
                    left[i],
                    right[i],
                    -1,
                    Guid.Empty,
                    starts[i],
                    (ulong)streams[i].Value.Length
                );
            }

            for (int i = n + 1; i < entries.Length / 128; i++)
            {
                Put32(entries, i * 128 + 68, Free);
                Put32(entries, i * 128 + 72, Free);
                Put32(entries, i * 128 + 76, Free);
            }

            Place(file, size, directoryStart, entries);
            Place(file, size, miniFatStart, miniFat.SelectMany(v => U32(v)).ToArray());
            Place(file, size, miniStart, mini.ToArray());

            for (int k = 0; k < big.Count; k++)
            {
                Place(file, size, (int)starts[big[k]], streams[big[k]].Value);
            }

            return file;
        }

        /// <summary>A stream of a compound file, by its name.</summary>
        internal static KeyValuePair<string, byte[]> Stream(string name, byte[] data) =>
            new KeyValuePair<string, byte[]>(name, data);

        /// <summary>
        /// The binary of an OLE2FRAME: AutoCAD's head (its signature's first byte, then 0x55; the corners upper left, upper right,
        /// lower right and lower left; the natural size 79 by 52 hundredths of a millimetre; 1), MFC's 256, item 1, aspect 0, no
        /// moniker, drawing aspect 1 and the length of the compound file, then the compound file.
        /// </summary>
        internal static byte[] Head(GeoPoint3[] corners, byte signature, byte[] compound)
        {
            var head = new byte[128];
            head[0] = signature;
            head[1] = 0x55;

            for (int k = 0; k < 4; k++)
            {
                BitConverter.GetBytes(corners[k].X).CopyTo(head, 2 + 24 * k);
                BitConverter.GetBytes(corners[k].Y).CopyTo(head, 10 + 24 * k);
                BitConverter.GetBytes(corners[k].Z).CopyTo(head, 18 + 24 * k);
            }

            Put16(head, 98, 79);
            Put16(head, 100, 52);
            Put32(head, 102, 1);
            Put32(head, 106, 256);
            Put32(head, 110, 1);
            Put32(head, 114, 0);
            Put16(head, 118, 0);
            Put32(head, 120, 1);
            Put32(head, 124, (uint)compound.Length);
            return head.Concat(compound).ToArray();
        }

        /// <summary>
        /// An OLE2FRAME record as AutoCAD 2021 writes one: 10 and 11 the head's upper left and lower right corners (or those of
        /// the frame given), its quality (73) unless below nought, its binary in lines of 32 bytes, and its extended data between
        /// OLEBEGIN and OLEEND where asked.
        /// </summary>
        internal static void Frame(
            StringBuilder b,
            string handle,
            bool paper,
            string userType,
            int type,
            byte[] binary,
            bool data,
            GeoPoint3[] groups = null,
            int quality = 2,
            string auto = "1",
            string locked = "1",
            string rotation = "0",
            string turned = "0"
        )
        {
            GeoPoint3[] corners = groups
                ?? new[] { Corner(binary, 0), Corner(binary, 1), Corner(binary, 2), Corner(binary, 3) };
            b.Append("  0\nOLE2FRAME\n  5\n")
                .Append(handle)
                .Append("\n330\n")
                .Append(paper ? "1B" : "17")
                .Append("\n100\nAcDbEntity\n");
            b.Append(paper ? " 67\n1\n" : string.Empty)
                .Append("  8\n0\n100\nAcDbOle2Frame\n 70\n2\n  3\n")
                .Append(userType)
                .Append('\n');
            Point(b, 10, corners[0]);
            Point(b, 11, corners[2]);
            b.Append(" 71\n").Append(type).Append("\n 72\n").Append(paper ? 1 : 0).Append('\n');
            b.Append(quality < 0 ? string.Empty : " 73\n" + quality + "\n")
                .Append(" 90\n")
                .Append(binary.Length)
                .Append('\n');

            for (int at = 0; at < binary.Length; at += 32)
            {
                b.Append("310\n")
                    .Append(
                        BitConverter.ToString(binary, at, Math.Min(32, binary.Length - at)).Replace("-", string.Empty)
                    )
                    .Append('\n');
            }

            b.Append("  1\nOLE\n");

            if (data)
            {
                b.Append("1001\nACAD\n1000\nOLEBEGIN\n");
                b.Append("1070\n70\n1070\n")
                    .Append(auto)
                    .Append("\n1070\n71\n1070\n")
                    .Append(locked)
                    .Append("\n1070\n40\n1040\n")
                    .Append(rotation)
                    .Append('\n');
                b.Append("1070\n41\n1040\n")
                    .Append(Text(corners[1].X - corners[0].X))
                    .Append("\n1070\n42\n1040\n")
                    .Append(Text(corners[0].Y - corners[3].Y))
                    .Append('\n');
                b.Append("1070\n72\n1070\n")
                    .Append(turned)
                    .Append("\n1070\n3\n1000\n\n1070\n90\n1071\n12\n1070\n43\n1040\n0.1666666666666666\n");
                b.Append("1070\n4\n1000\n\n1070\n91\n1071\n12\n1070\n44\n1040\n0.1666666666666666\n1000\nOLEEND\n");
            }
        }

        /// <summary>A corner of the head of a binary, 0 the upper left; the origin where the binary is too short.</summary>
        private static GeoPoint3 Corner(byte[] binary, int k) =>
            binary.Length < 98
                ? new GeoPoint3(0, 0, 0)
                : new GeoPoint3(
                    BitConverter.ToDouble(binary, 2 + 24 * k),
                    BitConverter.ToDouble(binary, 10 + 24 * k),
                    BitConverter.ToDouble(binary, 18 + 24 * k)
                );

        private static void Point(StringBuilder b, int code, GeoPoint3 p) =>
            b.Append(' ')
                .Append(code)
                .Append('\n')
                .Append(Text(p.X))
                .Append("\n ")
                .Append(code + 10)
                .Append('\n')
                .Append(Text(p.Y))
                .Append("\n ")
                .Append(code + 20)
                .Append('\n')
                .Append(Text(p.Z))
                .Append('\n');

        private static string Text(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        private static byte[] Moniker(Guid classId, byte[] data) => classId.ToByteArray().Concat(data).ToArray();

        /// <summary>A file moniker's data: its count of parent folders, its ANSI path, then its Unicode path where asked.</summary>
        private static byte[] FileMoniker(int anti, string path, bool unicode)
        {
            var b = new List<byte>();
            b.AddRange(U16((ushort)anti));
            b.AddRange(U32((uint)path.Length + 1));
            b.AddRange(Ansi(path));
            b.AddRange(U16(0xFFFF));
            b.AddRange(U16(0xDEAD));
            b.AddRange(new byte[20]);

            if (unicode)
            {
                byte[] text = Encoding.Unicode.GetBytes(path);
                b.AddRange(U32((uint)(6 + text.Length)));
                b.AddRange(U32((uint)text.Length));
                b.AddRange(U16(3));
                b.AddRange(text);
            }
            else
            {
                b.AddRange(U32(0));
            }

            return b.ToArray();
        }

        /// <summary>An item moniker's data: its delimiter and its item, each of its size and ANSI letters closed by a nought.</summary>
        private static byte[] ItemMoniker(string delimiter, string item) =>
            U32((uint)delimiter.Length + 1)
                .Concat(Ansi(delimiter))
                .Concat(U32((uint)item.Length + 1))
                .Concat(Ansi(item))
                .ToArray();

        private static void Entry(
            byte[] entries,
            int index,
            string name,
            byte type,
            bool red,
            int left,
            int right,
            int child,
            Guid classId,
            uint start,
            ulong size
        )
        {
            int at = index * 128;
            byte[] text = Encoding.Unicode.GetBytes(name + "\0");
            text.CopyTo(entries, at);
            Put16(entries, at + 64, text.Length);
            entries[at + 66] = type;
            entries[at + 67] = (byte)(red ? 0 : 1);
            Put32(entries, at + 68, left < 0 ? 0xFFFFFFFF : (uint)(left + 1));
            Put32(entries, at + 72, right < 0 ? 0xFFFFFFFF : (uint)(right + 1));
            Put32(entries, at + 76, child < 0 ? 0xFFFFFFFF : (uint)(child + 1));
            classId.ToByteArray().CopyTo(entries, at + 80);
            Put32(entries, at + 116, start);
            BitConverter.GetBytes(size).CopyTo(entries, at + 120);
        }

        private static void Place(byte[] file, int size, int first, byte[] data)
        {
            if (data.Length > 0)
            {
                data.CopyTo(file, (first + 1) * size);
            }
        }

        private static byte[] Prefixed(string text) => U32((uint)text.Length + 1).Concat(Ansi(text)).ToArray();

        private static byte[] Ansi(string text) => Encoding.ASCII.GetBytes(text + "\0");

        private static byte[] U16(int value) => new[] { (byte)value, (byte)(value >> 8) };

        private static byte[] U32(uint value) => BitConverter.GetBytes(value);

        private static IEnumerable<byte> I32s(params int[] values) => values.SelectMany(v => BitConverter.GetBytes(v));

        private static void Put16(byte[] bytes, int at, int value)
        {
            bytes[at] = (byte)value;
            bytes[at + 1] = (byte)(value >> 8);
        }

        private static void Put32(byte[] bytes, int at, uint value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
    }
}
