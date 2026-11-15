using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Puts the sections of a DWG file into the container of R2007 (AC1021), the mirror of
    /// <see cref="DwgContainer"/>'s reading of it (DwgContainer.R2007.cs): a file header of 0x80 bytes, then 0x400 bytes
    /// of the header proper, compressed and coded by Reed-Solomon (255, 239) in three interleaved blocks, then the pages
    /// from 0x480 on: the page map and its copy, the data pages of the sections in turn, the section map and its copy,
    /// and a copy of the 0x400 bytes of the header at the end. A data page of coding 4 is its data, compressed where that
    /// makes it shorter (<see cref="DwgLz21.Compress"/>), coded by Reed-Solomon (255, 251) in interleaved blocks, and takes
    /// 255 bytes of every 251 of data, to a multiple of 32; a page of coding 1 is its data stored as it is, filled out to
    /// the page size of its section, and 32 bytes. A system page, the page map or the section map, holds its data,
    /// compressed where that makes it shorter, as many times over as its place takes, in blocks of Reed-Solomon (255, 239).
    /// Every sum is written as <see cref="DwgContainer"/> checks it: the sum of the data of a page, the CRC of 64 bits of the
    /// bytes stored, and the CRCs of the page maps and of the header.
    /// </summary>
    internal sealed class DwgContainerWriter21
    {
        /// <summary>The place of the first page in the file; the offsets of the pages count from it.</summary>
        private const int PagesStart = 0x480;

        /// <summary>The size of the header proper, at 0x80.</summary>
        private const int HeaderSize = 0x400;

        /// <summary>The most a page of a large section holds, and what the file header names as the page size.</summary>
        public const int LargePage = 0xF800;

        /// <summary>The number AutoCAD writes for the hash of each section in the section map, found in its files.</summary>
        private static readonly Dictionary<string, long> Hashes = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["AcDb:AppInfoHistory"] = 0x96DE0737,
            ["AcDb:AppInfo"] = 0x3FA0043E,
            ["AcDb:Preview"] = 0x40AA0473,
            ["AcDb:SummaryInfo"] = 0x717A060F,
            ["AcDb:RevHistory"] = 0x60A205B3,
            ["AcDb:AcDbObjects"] = 0x674C05A9,
            ["AcDb:ObjFreeSpace"] = 0x77E2061F,
            ["AcDb:Template"] = 0x4A1404CE,
            ["AcDb:Handles"] = 0x3F6E0450,
            ["AcDb:Classes"] = 0x3F54045F,
            ["AcDb:AuxHeader"] = 0x54F0050A,
            ["AcDb:Header"] = 0x32B803D9,
        };

        private readonly int _codePage;
        private readonly List<Section> _sections = new List<Section>();

        /// <summary>A container of AC1021 whose file header names the code page number <paramref name="codePage"/> (30 for ANSI_1252).</summary>
        public DwgContainerWriter21(int codePage)
        {
            _codePage = codePage;
        }

        /// <summary>
        /// Adds a section, its pages after those of the sections added before it: its name, such as <c>AcDb:Header</c>, its
        /// data, the most one page of it holds (the page size of the section map; a section of coding 1 has one page, as
        /// large as its data needs) and its coding, 4 for the pages coded by Reed-Solomon in interleaved blocks and 1 for
        /// those that are not.
        /// </summary>
        public void Add(string name, byte[] data, int pageSize, int coding)
        {
            if (!Hashes.ContainsKey(name))
            {
                throw new ArgumentException("The section " + name + " is not one a file of AC1021 has.", nameof(name));
            }

            _sections.Add(
                new Section
                {
                    Name = name,
                    Data = data,
                    PageSize = coding == 1 || pageSize < LargePage ? Math.Max(pageSize, data.Length) : pageSize,
                    Coding = coding,
                }
            );
        }

        /// <summary>Replaces the data of a section already added with other data of the same length.</summary>
        public void Replace(string name, byte[] data)
        {
            Section section = _sections.Find(s => s.Name == name);

            if (section == null || section.Data.Length != data.Length)
            {
                throw new ArgumentException(
                    "There is no section " + name + " of that length to replace.",
                    nameof(name)
                );
            }

            section.Data = data;
            section.Pages = null;
        }

        /// <summary>
        /// The place in the file where the data of the first section begins, which AutoCAD's preview names for its pictures:
        /// after the header and the page map with its copy, whose size follows from the number of pages. The first section
        /// added is the one, and the others are added before this is asked.
        /// </summary>
        public long PreviewAddress()
        {
            int pages = 4;

            foreach (Section section in _sections)
            {
                pages += Math.Max(1, (section.Data.Length + section.PageSize - 1) / section.PageSize);
            }

            return PagesStart + (2L * SystemPageSize(16 * pages));
        }

        /// <summary>The bytes of the file.</summary>
        public byte[] ToArray()
        {
            // The data pages, numbered from 3 in the order of the sections.
            var data = new List<Page>();
            int number = 3;

            foreach (Section section in _sections)
            {
                section.Pages = section.Pages ?? Pages(section);

                foreach (Page page in section.Pages)
                {
                    page.Number = number++;
                    data.Add(page);
                }
            }

            // The section map and its copy follow the data pages, the page map and its copy are numbered after them
            // (AutoCAD leaves two numbers between the section map and the page map).
            int sectionMapNumber = number++;
            int sectionMapCopyNumber = number++;
            int pageMapNumber = number + 2;
            int pageMapCopyNumber = number + 3;
            int pageCount = data.Count + 4;
            byte[] map = SectionMap();
            SystemPage sectionMap = MakeSystemPage(map);

            // The page map names the size of its own page and of its copy, which follows from its own size alone.
            var mapStream = new MemoryStream();
            int pageMapSlot = SystemPageSize(16 * pageCount);
            Long64(mapStream, pageMapSlot);
            Long64(mapStream, pageMapNumber);
            Long64(mapStream, pageMapSlot);
            Long64(mapStream, pageMapCopyNumber);

            foreach (Page page in data)
            {
                Long64(mapStream, page.Bytes.Length);
                Long64(mapStream, page.Number);
            }

            Long64(mapStream, sectionMap.Slot.Length);
            Long64(mapStream, sectionMapNumber);
            Long64(mapStream, sectionMap.Slot.Length);
            Long64(mapStream, sectionMapCopyNumber);
            SystemPage pageMap = MakeSystemPage(mapStream.ToArray());

            var pages = new MemoryStream();
            pages.Write(pageMap.Slot, 0, pageMap.Slot.Length);
            pages.Write(pageMap.Slot, 0, pageMap.Slot.Length);

            foreach (Page page in data)
            {
                page.Place = pages.Position;
                pages.Write(page.Bytes, 0, page.Bytes.Length);
            }

            pages.Write(sectionMap.Slot, 0, sectionMap.Slot.Length);
            pages.Write(sectionMap.Slot, 0, sectionMap.Slot.Length);
            long copyAt = pages.Position;
            long size = PagesStart + copyAt + HeaderSize;

            // The header proper: 0x110 bytes of the places and sums of the maps. The values that AutoCAD writes at random
            // (the seeds at 0x20, 0xE0, 0xF8 and 0x100, and the key of the frame) are those of a file it wrote, so that the
            // same drawing gives the same bytes.
            var meta = new byte[0x110];
            Put64(meta, 0x00, 0x70);
            Put64(meta, 0x08, size);
            Put64(meta, 0x10, (long)pageMap.StoredCrc);
            Put64(meta, 0x18, pageMap.Repeat);
            Put64(meta, 0x20, unchecked((long)0xF4821D2391699644));
            Put64(meta, 0x28, pageMap.Slot.Length);
            Put64(meta, 0x30, pageMapCopyNumber);
            Put64(meta, 0x38, 0);
            Put64(meta, 0x40, pageMapNumber);
            Put64(meta, 0x48, copyAt);
            Put64(meta, 0x50, pageMap.Stored);
            Put64(meta, 0x58, 16 * pageCount);
            Put64(meta, 0x60, pageCount);
            Put64(meta, 0x68, pageMapCopyNumber);
            Put64(meta, 0x70, 0x20);
            Put64(meta, 0x78, 0x40);
            Put64(meta, 0x80, (long)pageMap.PlainCrc);
            Put64(meta, 0x88, LargePage);
            Put64(meta, 0x90, 4);
            Put64(meta, 0x98, 1);
            Put64(meta, 0xA0, _sections.Count + 1);
            Put64(meta, 0xA8, (long)sectionMap.PlainCrc);
            Put64(meta, 0xB0, sectionMap.Stored);
            Put64(meta, 0xB8, sectionMapCopyNumber);
            Put64(meta, 0xC0, sectionMapNumber);
            Put64(meta, 0xC8, map.Length);
            Put64(meta, 0xD0, (long)sectionMap.StoredCrc);
            Put64(meta, 0xD8, sectionMap.Repeat);
            Put64(meta, 0xE0, 0x309369F353442281);
            Put64(meta, 0xE8, 0x60100);
            Put64(meta, 0xF0, 0);
            Put64(meta, 0xF8, unchecked((long)0xD10760A69A3530C5));
            Put64(meta, 0x100, unchecked((long)0x8BB8DD6EAC62281D));
            Put64(meta, 0x108, (long)DwgCrc.Of64(meta, 0, meta.Length, DwgCrc.Seed2(0, meta.Length), false));

            // The frame of the header proper: the CRC of the key and of the key turned by itself, the key, the CRC of the
            // data compressed, its length, and the data; AutoCAD reads the header only where its data is compressed.
            byte[] packed = DwgLz21.Compress(meta)
                ?? throw new InvalidOperationException("The header of the file did not compress.");
            var coded = new byte[0x20 + packed.Length];
            ulong key = 0x869CE63076759468;
            Put64(coded, 8, (long)key);
            int turn = (int)(key & 0x1F);
            ulong turned = turn == 0 ? key : (key << turn) | (key >> (64 - turn));
            var check = new byte[16];
            Array.Copy(coded, 8, check, 0, 8);
            Put64(check, 8, (long)turned);
            Put64(coded, 0, (long)DwgCrc.Of64(check, 0, 16, DwgCrc.Seed1(0, 16), false));
            Put64(coded, 0x10, (long)DwgCrc.Of64(packed, 0, packed.Length, DwgCrc.Seed2(0, packed.Length), false));
            Put32(coded, 0x18, (uint)packed.Length);
            Array.Copy(packed, 0, coded, 0x20, packed.Length);
            byte[] block = DwgReedSolomon.Of239.Interleave(coded, 3);
            var header = new byte[HeaderSize];
            Array.Copy(block, header, block.Length);

            // The file: its own header of 0x80 bytes, the header proper, the pages and the copy of the header proper.
            var file = new byte[size];
            Encoding.ASCII.GetBytes("AC1021").CopyTo(file, 0);
            file[0x0B] = 0x32;
            file[0x0C] = 3;
            Put32(file, 0x0D, (uint)AddressOf("AcDb:Preview"));
            file[0x11] = 33;
            file[0x12] = 174;
            file[0x13] = (byte)_codePage;
            file[0x14] = (byte)(_codePage >> 8);
            file[0x16] = 33;
            file[0x17] = 174;
            Put32(file, 0x20, (uint)AddressOf("AcDb:SummaryInfo"));
            Put32(file, 0x28, 0x80);
            Put32(file, 0x2C, (uint)AddressOf("AcDb:AppInfo"));
            Put32(file, 0x30, (uint)AddressOf("AcDb:AppInfoHistory"));
            Put32(file, 0x34, 0x202);
            Put32(file, 0x38, 0xE0);
            Array.Copy(header, 0, file, 0x80, HeaderSize);
            pages.ToArray().CopyTo(file, PagesStart);
            Array.Copy(header, 0, file, size - HeaderSize, HeaderSize);
            return file;
        }

        /// <summary>The pages of a section: its data cut at the page size, each page coded as its section says.</summary>
        private static List<Page> Pages(Section section)
        {
            var pages = new List<Page>();
            int count = Math.Max(1, (section.Data.Length + section.PageSize - 1) / section.PageSize);

            for (int i = 0; i < count; i++)
            {
                int start = i * section.PageSize;
                var plain = new byte[Math.Max(0, Math.Min(section.PageSize, section.Data.Length - start))];
                Array.Copy(section.Data, start, plain, 0, plain.Length);
                var page = new Page
                {
                    Offset = start,
                    Plain = plain,
                    Sum = DwgCrc.OfPage21(plain, 0, plain.Length, 0),
                };

                if (section.Coding == 4)
                {
                    page.Stored = DwgLz21.Compress(plain) ?? plain;
                    int aligned = (page.Stored.Length + 7) & ~7;
                    int blocks = (aligned + 250) / 251;
                    page.Bytes = Fill(DwgReedSolomon.Of251.Interleave(page.Stored, blocks), Round32(blocks * 255));
                }
                else
                {
                    // The data, padded to eight bytes and followed by the parity of each of its blocks of 251 bytes (the
                    // last made up with zeros), as AutoCAD writes it, in the 32 bytes it adds to the page size or the more the
                    // parity takes; what is left of the place is zero.
                    page.Stored = plain;
                    int aligned = (plain.Length + 7) & ~7;
                    int blocks = (aligned + 250) / 251;
                    var bytes = new byte[section.PageSize + Math.Max(32, Round32(4 * blocks))];
                    Array.Copy(plain, bytes, plain.Length);
                    var block = new byte[255];
                    var padded = new byte[251];

                    for (int j = 0; j < blocks; j++)
                    {
                        int left = Math.Min(251, aligned - (j * 251));
                        Array.Clear(padded, 0, padded.Length);
                        Array.Copy(bytes, j * 251, padded, 0, left);
                        DwgReedSolomon.Of251.Encode(padded, 0, block);
                        Array.Copy(block, 251, bytes, aligned + (j * 4), 4);
                    }

                    page.Bytes = bytes;
                }

                page.Crc = DwgCrc.Of64(page.Stored, 0, page.Stored.Length, DwgCrc.Seed1(0, page.Stored.Length), true);
                pages.Add(page);
            }

            return pages;
        }

        /// <summary>The place in the file of the first page of a section, as AutoCAD's file header names it; 0 where there is no such section.</summary>
        private long AddressOf(string name)
        {
            foreach (Section section in _sections)
            {
                if (section.Name == name && section.Pages.Count > 0)
                {
                    return PagesStart + section.Pages[0].Place;
                }
            }

            return 0;
        }

        /// <summary>
        /// The section map: for each section one record of its size, its page size, its hash, its coding and the name, then
        /// a record for each page; the sections of coding 1, which come first in the file, in the opposite order, as AutoCAD
        /// writes them; then the empty section of coding 4.
        /// </summary>
        private byte[] SectionMap()
        {
            var ordered = new List<Section>();

            for (int i = _sections.Count - 1; i >= 0; i--)
            {
                if (_sections[i].Coding == 1)
                {
                    ordered.Add(_sections[i]);
                }
            }

            ordered.AddRange(_sections.FindAll(s => s.Coding != 1));
            var map = new MemoryStream();

            foreach (Section section in ordered)
            {
                byte[] name = Encoding.Unicode.GetBytes(section.Name + "\0");
                Long64(map, section.Data.Length);
                Long64(map, section.PageSize);
                Long64(map, 0);
                Long64(map, Hashes[section.Name]);
                Long64(map, name.Length);
                Long64(map, 0);
                Long64(map, section.Coding);
                Long64(map, section.Pages.Count);
                map.Write(name, 0, name.Length);

                foreach (Page page in section.Pages)
                {
                    Long64(map, page.Offset);
                    Long64(map, page.Plain.Length);
                    Long64(map, page.Number);
                    Long64(map, page.Plain.Length);
                    Long64(map, page.Stored.Length);
                    Long64(map, page.Sum);
                    Long64(map, (long)page.Crc);
                }
            }

            Long64(map, 0);
            Long64(map, LargePage);
            Long64(map, 0);
            Long64(map, 0);
            Long64(map, 0);
            Long64(map, 0);
            Long64(map, 4);
            Long64(map, 0);
            return map.ToArray();
        }

        /// <summary>
        /// A system page, the page map or the section map: its data compressed where that makes it shorter, padded to eight
        /// bytes and repeated as many times as the place of the page takes, coded by Reed-Solomon (255, 239) in blocks laid
        /// one byte after the other. The place is that of <see cref="SystemPageSize"/>, which depends on the size of the
        /// data alone; of what it holds, the file header names the repeat and the CRCs.
        /// </summary>
        private static SystemPage MakeSystemPage(byte[] data)
        {
            byte[] stored = DwgLz21.Compress(data) ?? data;
            int slot = SystemPageSize(data.Length);
            int aligned = (stored.Length + 7) & ~7;
            int repeat = Math.Max(1, (slot / 255 * 239) / aligned);
            var copies = new byte[aligned * repeat];

            for (int i = 0; i < repeat; i++)
            {
                Array.Copy(stored, 0, copies, i * aligned, stored.Length);
            }

            int blocks = (copies.Length + 238) / 239;
            return new SystemPage
            {
                Slot = Fill(DwgReedSolomon.Of239.Interleave(copies, blocks), slot),
                Stored = stored.Length,
                Repeat = repeat,
                StoredCrc = DwgCrc.Of64(stored, 0, stored.Length, DwgCrc.Seed1(0, stored.Length), true),
                PlainCrc = DwgCrc.Of64(data, 0, data.Length, DwgCrc.Seed1(0, data.Length), true),
            };
        }

        /// <summary>
        /// The bytes a system page of <paramref name="length"/> bytes of data takes in the file: blocks of 255 bytes,
        /// at least 4 and enough to hold the data twice over, to a multiple of 32.
        /// </summary>
        private static int SystemPageSize(int length)
        {
            int blocks = Math.Max(4, ((2 * length) + 238) / 239);
            return Round32(blocks * 255);
        }

        private static byte[] Fill(byte[] bytes, int size)
        {
            if (bytes.Length == size)
            {
                return bytes;
            }

            var filled = new byte[size];
            Array.Copy(bytes, filled, Math.Min(bytes.Length, size));
            return filled;
        }

        private static int Round32(int size) => (size + 31) & ~31;

        private static void Put32(byte[] data, int at, uint value)
        {
            data[at] = (byte)value;
            data[at + 1] = (byte)(value >> 8);
            data[at + 2] = (byte)(value >> 16);
            data[at + 3] = (byte)(value >> 24);
        }

        private static void Put64(byte[] data, int at, long value)
        {
            Put32(data, at, (uint)value);
            Put32(data, at + 4, (uint)(value >> 32));
        }

        private static void Long64(Stream stream, long value)
        {
            for (int i = 0; i < 8; i++)
            {
                stream.WriteByte((byte)(value >> (8 * i)));
            }
        }

        private sealed class Section
        {
            public string Name;
            public byte[] Data;
            public int PageSize;
            public int Coding;
            public List<Page> Pages;
        }

        private sealed class Page
        {
            public int Number;
            public long Offset;
            public long Place;

            /// <summary>The data of the page, and the bytes that are stored of it: the data compressed, or the data.</summary>
            public byte[] Plain;
            public byte[] Stored;

            /// <summary>What the page takes in the file.</summary>
            public byte[] Bytes;
            public long Sum;
            public ulong Crc;
        }

        private sealed class SystemPage
        {
            public byte[] Slot;
            public int Stored;
            public int Repeat;
            public ulong StoredCrc;
            public ulong PlainCrc;
        }
    }
}
