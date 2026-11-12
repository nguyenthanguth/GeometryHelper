using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The container of R2004 and later (AC1018, AC1024, AC1027, AC1032): a file header whose second part is masked, then
    /// pages, each on a 32-byte boundary. Two system pages map the rest: the page map gives the place of every page, the
    /// section map gives the pages of each named section. The pages of a section are decompressed and joined, and the
    /// sections the reader needs are taken from them: the header variables, the classes, the object map and the objects.
    /// </content>
    internal sealed partial class DwgContainer
    {
        /// <summary>The 0x6C bytes the second part of the file header is masked with: the start of the series of a linear congruential generator from 1.</summary>
        private static readonly byte[] HeaderMask = MakeHeaderMask();

        private const uint PageMapType = 0x41630E3B;
        private const uint SectionMapType = 0x4163003B;
        private const uint DataPageType = 0x4163043B;

        /// <summary>
        /// The most the sections of one file may decompress to, as a multiple of the size of the file, over a fixed
        /// allowance. AutoCAD's drawings decompress to a few times their size; a page map that claims more is taken as
        /// broken, so a hostile length cannot make the reader allocate without bound.
        /// </summary>
        private const long Expansion = 64;

        private const long Allowance = 8L << 20;

        /// <summary>The largest page a section may name; AutoCAD's are 0x7400 bytes, and a few small sections use less.</summary>
        private const int MaxPageSize = 1 << 20;

        private static byte[] MakeHeaderMask()
        {
            var mask = new byte[0x6C];
            uint seed = 1;

            for (int i = 0; i < mask.Length; i++)
            {
                seed = (seed * 0x343FD) + 0x269EC3;
                mask[i] = (byte)(seed >> 16);
            }

            return mask;
        }

        private static uint U32(byte[] data, long at) =>
            (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));

        private static long U64(byte[] data, long at) => (long)(U32(data, at) | ((ulong)U32(data, at + 4) << 32));

        private static DwgContainer ReadR18(byte[] file, string version)
        {
            if (file.LongLength < 0x100)
            {
                throw new FormatException("The DWG file is cut short: it ends inside its file header.");
            }

            int maintenance = file[0x0B];
            int codePage = file[0x13] | (file[0x14] << 8);
            uint security = U32(file, 0x18);

            if ((security & 1) != 0)
            {
                throw new NotSupportedException("The DWG file is protected by a password; such files are not read.");
            }

            var head = new byte[0x6C];

            for (int i = 0; i < head.Length; i++)
            {
                head[i] = (byte)(file[0x80 + i] ^ HeaderMask[i]);
            }

            if (Encoding.ASCII.GetString(head, 0, 11) != "AcFssFcAJMB" || head[11] != 0)
            {
                throw new FormatException("The DWG file header is broken: it does not hold the name of its data.");
            }

            uint crc = U32(head, 0x68);
            head[0x68] = head[0x69] = head[0x6A] = head[0x6B] = 0;

            if (DwgCrc.Of32(head, 0, head.Length, 0) != crc)
            {
                throw new FormatException("The DWG file header has a wrong check sum.");
            }

            long pageMapAt = U64(head, 0x54) + 0x100;
            uint sectionMapId = U32(head, 0x5C);
            long budget = Math.Min(int.MaxValue, (file.LongLength * Expansion) + Allowance);

            byte[] pageMapData = SystemPage(file, pageMapAt, PageMapType, "page map", ref budget);
            Dictionary<int, long[]> pages = ReadPageMap(pageMapData, file.LongLength);

            if (!pages.TryGetValue((int)sectionMapId, out long[] sectionMapPage))
            {
                throw new FormatException("The DWG page map does not place the section map.");
            }

            byte[] sectionMapData = SystemPage(file, sectionMapPage[0], SectionMapType, "section map", ref budget);
            Dictionary<string, Descriptor> sections = ReadSectionMap(sectionMapData);

            var container = new DwgContainer
            {
                Version = version,
                MaintenanceVersion = maintenance,
                CodePage = codePage >= 0 && codePage < CodePageNames.Length ? CodePageNames[codePage] : "ANSI_1252",
                Measurement = -1,
            };

            bool hasUnknown = (version != "AC1018" && maintenance > 3) || string.CompareOrdinal(version, "AC1027") > 0;
            container.Header = Section(
                Join(file, pages, Need(sections, "AcDb:Header"), ref budget),
                HeaderStart,
                HeaderEnd,
                "header variables",
                hasUnknown
            );
            container.Classes = Section(
                Join(file, pages, Need(sections, "AcDb:Classes"), ref budget),
                ClassesStart,
                ClassesEnd,
                "classes",
                hasUnknown
            );
            byte[] handles = Join(file, pages, Need(sections, "AcDb:Handles"), ref budget);
            container.Objects = Join(file, pages, Need(sections, "AcDb:AcDbObjects"), ref budget);
            container.ObjectMap = ReadObjectMap(handles, 0, handles.LongLength, container.Objects.LongLength);

            if (sections.TryGetValue("AcDb:AcDsPrototype_1b", out Descriptor store))
            {
                container.DataStore = Join(file, pages, store, ref budget);
            }

            if (sections.TryGetValue("AcDb:Template", out Descriptor template))
            {
                byte[] data = Join(file, pages, template, ref budget);

                if (data.Length >= 2)
                {
                    int length = data[0] | (data[1] << 8);

                    if (length >= 0 && 2 + length + 2 <= data.Length)
                    {
                        container.Measurement = data[2 + length] | (data[3 + length] << 8);
                    }
                }
            }

            return container;
        }

        /// <summary>
        /// Every named section of a file of R2004 and later (not R2007), its pages decompressed and joined, in the order of
        /// the section map; <paramref name="layout"/> gives each its section id, its page size and its compression (1 none,
        /// 2 compressed). For the writer's tests, which put AutoCAD's own sections into a container of the writer's.
        /// </summary>
        internal static List<KeyValuePair<string, byte[]>> SectionsOf(byte[] file, out Dictionary<string, int[]> layout)
        {
            var head = new byte[0x6C];

            for (int i = 0; i < head.Length; i++)
            {
                head[i] = (byte)(file[0x80 + i] ^ HeaderMask[i]);
            }

            long budget = Math.Min(int.MaxValue, (file.LongLength * Expansion) + Allowance);
            Dictionary<int, long[]> pages = ReadPageMap(
                SystemPage(file, U64(head, 0x54) + 0x100, PageMapType, "page map", ref budget),
                file.LongLength
            );
            byte[] map = SystemPage(file, pages[(int)U32(head, 0x5C)][0], SectionMapType, "section map", ref budget);
            Dictionary<string, Descriptor> descriptors = ReadSectionMap(map);
            var sections = new List<KeyValuePair<string, byte[]>>();
            layout = new Dictionary<string, int[]>(StringComparer.Ordinal);
            int count = (int)U32(map, 0);
            int at = 20;

            for (int i = 0; i < count; i++)
            {
                int end = Array.IndexOf(map, (byte)0, at + 32, 64);
                string name = Encoding.ASCII.GetString(map, at + 32, (end < 0 ? at + 96 : end) - (at + 32));

                if (name.Length > 0)
                {
                    layout[name] = new[] { (int)U32(map, at + 24), (int)U32(map, at + 12), (int)U32(map, at + 20) };
                    sections.Add(
                        new KeyValuePair<string, byte[]>(name, Join(file, pages, descriptors[name], ref budget))
                    );
                }

                at += 96 + (16 * (int)U32(map, at + 8));
            }

            return sections;
        }

        /// <summary>
        /// A system page at <paramref name="at"/>: a head of five raw longs (its type, the size decompressed, the size
        /// compressed, the kind of compression, the check sum), then the compressed data, decompressed.
        /// </summary>
        private static byte[] SystemPage(byte[] file, long at, uint type, string name, ref long budget)
        {
            if (at < 0x100 || at + 0x14 > file.LongLength)
            {
                throw new FormatException("The DWG " + name + " lies outside the file.");
            }

            uint size = U32(file, at + 4);
            uint compressed = U32(file, at + 8);
            uint kind = U32(file, at + 12);
            uint sum = U32(file, at + 16);

            if (U32(file, at) != type || kind != 2 || at + 0x14 + compressed > file.LongLength || size > budget)
            {
                throw new FormatException("The DWG " + name + " is broken.");
            }

            var headCopy = new byte[0x14];
            Array.Copy(file, at, headCopy, 0, 0x10);
            uint seed = DwgCrc.OfPage(headCopy, 0, 0x14, 0);

            if (DwgCrc.OfPage(file, at + 0x14, compressed, seed) != sum)
            {
                throw new FormatException("The DWG " + name + " has a wrong check sum.");
            }

            budget -= size;
            var data = new byte[size];

            if (DwgLz77.Decompress(file, at + 0x14, compressed, data) != size)
            {
                throw new FormatException("The DWG " + name + " decompresses to another size than it names.");
            }

            return data;
        }

        /// <summary>
        /// The page map: for each page its number and its size, the first page at 0x100 and each after the one before; a
        /// negative number marks a gap, which four more longs follow. Gives the address and the size of each page by number.
        /// </summary>
        private static Dictionary<int, long[]> ReadPageMap(byte[] data, long fileLength)
        {
            var pages = new Dictionary<int, long[]>();
            long address = 0x100;
            int at = 0;

            while (at + 8 <= data.Length)
            {
                int number = (int)U32(data, at);
                long size = U32(data, at + 4);
                at += 8;

                if (number < 0)
                {
                    at += 16;
                }
                else if (address + size <= fileLength)
                {
                    pages[number] = new[] { address, size };
                }

                address += size;
            }

            return pages;
        }

        /// <summary>A section of the section map: its size, the size of its pages, whether they are compressed, and its pages as number and start in the section.</summary>
        private sealed class Descriptor
        {
            public string Name;
            public long Size;
            public int PageSize;
            public bool Compressed;
            public int Encrypted;
            public List<long[]> Pages = new List<long[]>();
        }

        private static Dictionary<string, Descriptor> ReadSectionMap(byte[] data)
        {
            var sections = new Dictionary<string, Descriptor>(StringComparer.Ordinal);

            if (data.Length < 20)
            {
                throw new FormatException("The DWG section map is cut short.");
            }

            int count = (int)U32(data, 0);
            int at = 20;

            for (int i = 0; i < count; i++)
            {
                if (at + 96 > data.Length)
                {
                    throw new FormatException("The DWG section map is cut short.");
                }

                var one = new Descriptor
                {
                    Size = U64(data, at),
                    PageSize = (int)U32(data, at + 12),
                    Compressed = U32(data, at + 20) == 2,
                    Encrypted = (int)U32(data, at + 28),
                };

                int pageCount = (int)U32(data, at + 8);
                int end = Array.IndexOf(data, (byte)0, at + 32, 64);
                one.Name = Encoding.ASCII.GetString(data, at + 32, (end < 0 ? at + 96 : end) - (at + 32));
                at += 96;

                if (
                    pageCount < 0
                    || pageCount > (data.Length - at) / 16
                    || one.Size < 0
                    || one.PageSize <= 0
                    || one.PageSize > MaxPageSize
                )
                {
                    throw new FormatException("The DWG section map is broken at the section " + one.Name + ".");
                }

                for (int j = 0; j < pageCount; j++)
                {
                    one.Pages.Add(new[] { (long)(int)U32(data, at), U64(data, at + 8) });
                    at += 16;
                }

                if (one.Name.Length > 0)
                {
                    sections[one.Name] = one;
                }
            }

            return sections;
        }

        private static Descriptor Need(Dictionary<string, Descriptor> sections, string name)
        {
            if (!sections.TryGetValue(name, out Descriptor found))
            {
                throw new FormatException("The DWG file has no section " + name + ".");
            }

            return found;
        }

        /// <summary>
        /// The bytes of a section: its pages decompressed and laid at their starts, the gaps between them zero. Each data
        /// page has a head of 32 bytes masked by its address: its type, its section, the size of its data, its own size, its
        /// start in the section, and the check sums of the head and of the data.
        /// </summary>
        private static byte[] Join(byte[] file, Dictionary<int, long[]> pages, Descriptor section, ref long budget)
        {
            if (section.Encrypted == 1)
            {
                throw new NotSupportedException(
                    "The DWG section " + section.Name + " is encrypted; such files are not read."
                );
            }

            if (section.Size > budget)
            {
                throw new FormatException(
                    "The DWG section " + section.Name + " names a size beyond what the file can hold."
                );
            }

            budget -= section.Size;
            var data = new byte[section.Size];
            var page = new byte[section.PageSize];

            foreach (long[] entry in section.Pages)
            {
                if (!pages.TryGetValue((int)entry[0], out long[] place))
                {
                    throw new FormatException(
                        "The DWG section " + section.Name + " has a page the page map does not place."
                    );
                }

                long at = place[0];

                if (at + 32 > file.LongLength)
                {
                    throw new FormatException("The DWG section " + section.Name + " has a page outside the file.");
                }

                var head = new byte[32];
                uint mask = 0x4164536B ^ (uint)at;

                for (int i = 0; i < 32; i += 4)
                {
                    uint value = U32(file, at + i) ^ mask;
                    head[i] = (byte)value;
                    head[i + 1] = (byte)(value >> 8);
                    head[i + 2] = (byte)(value >> 16);
                    head[i + 3] = (byte)(value >> 24);
                }

                long size = U32(head, 8);
                long start = U64(head, 16);
                uint headSum = U32(head, 24);
                uint dataSum = U32(head, 28);

                if (
                    U32(head, 0) != DataPageType
                    || size > place[1]
                    || at + 32 + size > file.LongLength
                    || start != entry[1]
                    || start < 0
                    || start >= section.Size
                )
                {
                    throw new FormatException("The DWG section " + section.Name + " has a broken page.");
                }

                head[24] = head[25] = head[26] = head[27] = 0;

                if (DwgCrc.OfPage(file, at + 32, size, 0) != dataSum || DwgCrc.OfPage(head, 0, 32, dataSum) != headSum)
                {
                    throw new FormatException(
                        "The DWG section " + section.Name + " has a page with a wrong check sum."
                    );
                }

                int length;

                if (section.Compressed)
                {
                    Array.Clear(page, 0, page.Length);
                    length = DwgLz77.Decompress(file, at + 32, size, page);
                }
                else
                {
                    length = (int)Math.Min(size, page.Length);
                    Array.Copy(file, at + 32, page, 0, length);
                }

                Array.Copy(page, 0, data, start, Math.Min(Math.Max(length, 0), section.Size - start));
            }

            return data;
        }

        /// <summary>
        /// The data of a section of R2004 and later that stands between two sentinels: the start sentinel, the size as a raw
        /// long, from R2010 (from the maintenance release 4) a second raw long, the data, the check sum, the end sentinel.
        /// </summary>
        private static byte[] Section(byte[] section, byte[] start, byte[] end, string name, bool hasUnknown)
        {
            int extra = hasUnknown ? 4 : 0;

            if (section.Length < 38 + extra || !Matches(section, 0, start))
            {
                throw new FormatException("The DWG section of the " + name + " does not start with its sentinel.");
            }

            long dataSize = U32(section, 16);

            if (dataSize + 38 + extra > section.Length)
            {
                throw new FormatException("The DWG section of the " + name + " names more data than it holds.");
            }

            long crcAt = 20 + extra + dataSize;
            int crc = section[crcAt] | (section[crcAt + 1] << 8);

            if (crc != DwgCrc.Of(section, 16, 4 + extra + dataSize, DwgCrc.Seed))
            {
                throw new FormatException("The DWG section of the " + name + " has a wrong check sum.");
            }

            if (!Matches(section, crcAt + 2, end))
            {
                throw new FormatException("The DWG section of the " + name + " does not end with its sentinel.");
            }

            var data = new byte[dataSize];
            Array.Copy(section, 20 + extra, data, 0, dataSize);
            return data;
        }
    }
}
