using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Puts the sections of a DWG file into the container of R2004 and later (AC1018, AC1024, AC1027, AC1032), the mirror
    /// of <see cref="DwgContainer"/>'s reading of it: the file header with its masked second part, then the data pages of
    /// each section, then the section map and the page map. A data page holds at most the page size of its section; a
    /// compressed one is filled out to that size with zeros and compressed whole, as AutoCAD writes its pages, an
    /// uncompressed one is filled out with zeros. Each page stands on a 32-byte boundary, the gap filled with the series
    /// the file header is masked with, and every sum is written as the reader checks it.
    /// </summary>
    internal sealed class DwgContainerWriter
    {
        private const uint PageMapType = 0x41630E3B;
        private const uint SectionMapType = 0x4163003B;
        private const uint DataPageType = 0x4163043B;

        /// <summary>The series a file header is masked with and the gaps between pages are filled with: a linear congruential generator from 1.</summary>
        private static readonly byte[] Series = MakeSeries();

        private readonly string _version;
        private readonly int _maintenance;
        private readonly int _codePage;
        private readonly List<Section> _sections = new List<Section>();

        /// <summary>
        /// A container of <paramref name="version"/> (such as <c>AC1032</c>) whose file header names the maintenance release
        /// <paramref name="maintenance"/> and the code page number <paramref name="codePage"/> (30 for ANSI_1252).
        /// </summary>
        public DwgContainerWriter(string version, int maintenance, int codePage)
        {
            _version = version;
            _maintenance = maintenance;
            _codePage = codePage;
        }

        /// <summary>
        /// Adds a section, its pages written after those of the sections added before it: its name, such as
        /// <c>AcDb:Header</c>, its data, its number in the section map, the most a page of it holds, and whether its pages
        /// are compressed.
        /// </summary>
        public void Add(string name, byte[] data, int id, int pageSize, bool compressed)
        {
            _sections.Add(
                new Section
                {
                    Name = name,
                    Data = data,
                    Id = id,
                    PageSize = pageSize,
                    Compressed = compressed,
                }
            );
        }

        /// <summary>The bytes of the file.</summary>
        public byte[] ToArray()
        {
            var file = new MemoryStream();
            file.Write(new byte[0x100], 0, 0x100);
            var pages = new List<long[]>();

            foreach (Section section in _sections)
            {
                section.Pages.Clear();
                int count = Math.Max(1, (section.Data.Length + section.PageSize - 1) / section.PageSize);

                for (int i = 0; i < count; i++)
                {
                    int start = i * section.PageSize;
                    int length = Math.Min(section.PageSize, section.Data.Length - start);
                    var page = new byte[section.PageSize];
                    Array.Copy(section.Data, start, page, 0, Math.Max(0, length));
                    byte[] data = section.Compressed ? DwgLz77.Compress(page, 0, page.Length) : page;
                    long at = file.Position;
                    int number = pages.Count + 1;
                    int size = Round32(32 + data.Length);
                    uint dataSum = DwgCrc.OfPage(data, 0, data.Length, 0);
                    var head = new byte[32];
                    Put32(head, 0, DataPageType);
                    Put32(head, 4, (uint)section.Id);
                    Put32(head, 8, (uint)data.Length);
                    Put32(head, 12, (uint)size);
                    Put64(head, 16, start);
                    Put32(head, 28, dataSum);
                    Put32(head, 24, DwgCrc.OfPage(head, 0, 32, dataSum));
                    uint mask = 0x4164536B ^ (uint)at;

                    for (int k = 0; k < 32; k += 4)
                    {
                        Put32(head, k, Get32(head, k) ^ mask);
                    }

                    file.Write(head, 0, 32);
                    file.Write(data, 0, data.Length);
                    file.Write(Series, 0, size - 32 - data.Length);
                    pages.Add(new long[] { number, size });
                    section.Pages.Add(new long[] { number, data.Length, start });
                }
            }

            // The section map: a head of five longs, then each section by falling number, the empty section 0 first.
            var ordered = new List<Section>(_sections);
            ordered.Sort((a, b) => b.Id.CompareTo(a.Id));
            var map = new MemoryStream();
            Long(map, ordered.Count + 1);
            Long(map, 2);
            Long(map, 0x7400);
            Long(map, 0);
            Long(map, ordered.Count + 1);
            Descriptor(
                map,
                new Section
                {
                    Name = string.Empty,
                    Data = new byte[0],
                    Id = 0,
                    PageSize = 0x7400,
                    Compressed = true,
                }
            );

            foreach (Section section in ordered)
            {
                Descriptor(map, section);
            }

            long sectionMapAt = file.Position;
            int sectionMapNumber = pages.Count + 1;
            int sectionMapSize = SystemPage(file, SectionMapType, map.ToArray(), true);
            pages.Add(new long[] { sectionMapNumber, sectionMapSize });

            // The page map, its own page among those it places: its size is known before it is written, as it is stored
            // in one run of literal bytes, whose length does not hang on what they are.
            int pageMapNumber = pages.Count + 1;
            int pageMapData = (pages.Count + 1) * 8;
            int pageMapSize = Round32(20 + LiteralRunLength(pageMapData) + pageMapData + 3);
            pages.Add(new long[] { pageMapNumber, pageMapSize });
            var pageMap = new MemoryStream();

            foreach (long[] page in pages)
            {
                Long(pageMap, (int)page[0]);
                Long(pageMap, (int)page[1]);
            }

            long pageMapAt = file.Position;
            int written = SystemPage(file, PageMapType, pageMap.ToArray(), false);

            if (written != pageMapSize)
            {
                throw new InvalidOperationException(
                    "The page map took " + written + " bytes, not the " + pageMapSize + " it names."
                );
            }

            // The file header: its masked part at 0x80 and again at the end of the file.
            var head2 = new byte[0x6C];
            Encoding.ASCII.GetBytes("AcFssFcAJMB").CopyTo(head2, 0);
            Put32(head2, 0x0C, 0);
            Put32(head2, 0x10, 0x6C);
            Put32(head2, 0x14, 0x04);
            Put32(head2, 0x24, 1);
            Put32(head2, 0x28, (uint)pageMapNumber);
            Put64(head2, 0x2C, pageMapAt + pageMapSize - 0x100);
            Put64(head2, 0x34, file.Position);
            Put32(head2, 0x3C, 0);
            Put32(head2, 0x40, (uint)pages.Count);
            Put32(head2, 0x44, 0x20);
            Put32(head2, 0x48, 0x80);
            Put32(head2, 0x4C, 0x40);
            Put32(head2, 0x50, (uint)pageMapNumber);
            Put64(head2, 0x54, pageMapAt - 0x100);
            Put32(head2, 0x5C, (uint)sectionMapNumber);
            Put32(head2, 0x60, (uint)pageMapNumber);
            Put32(head2, 0x64, 0);
            Put32(head2, 0x68, DwgCrc.Of32(head2, 0, 0x6C, 0));

            for (int i = 0; i < head2.Length; i++)
            {
                head2[i] ^= Series[i];
            }

            file.Write(head2, 0, head2.Length);
            byte[] bytes = file.ToArray();
            Array.Copy(head2, 0, bytes, 0x80, head2.Length);
            Encoding.ASCII.GetBytes(_version).CopyTo(bytes, 0);
            bytes[0x0B] = (byte)_maintenance;
            bytes[0x0C] = 3;
            Put32(bytes, 0x0D, (uint)AddressOf("AcDb:Preview", pages));
            bytes[0x11] = (byte)DwgVersionNumber(_version);
            bytes[0x12] = (byte)_maintenance;
            bytes[0x13] = (byte)_codePage;
            bytes[0x14] = (byte)(_codePage >> 8);
            Put32(bytes, 0x20, (uint)AddressOf("AcDb:SummaryInfo", pages));
            Put32(bytes, 0x28, 0x80);
            Put32(bytes, 0x2C, (uint)AddressOf("AcDb:AppInfo", pages));
            return bytes;
        }

        /// <summary>The number AutoCAD writes for a version in the file header and the auxiliary header: 25 R2004, 29 R2010, 31 R2013, 33 R2018.</summary>
        public static int DwgVersionNumber(string version)
        {
            switch (version)
            {
                case "AC1015":
                    return 23;
                case "AC1018":
                    return 25;
                case "AC1021":
                    return 27;
                case "AC1024":
                    return 29;
                case "AC1027":
                    return 31;
                default:
                    return 33;
            }
        }

        /// <summary>The place of the data of the first page of a section, past its head; 0 where there is no such section.</summary>
        private long AddressOf(string name, List<long[]> pages)
        {
            foreach (Section section in _sections)
            {
                if (section.Name != name || section.Pages.Count == 0)
                {
                    continue;
                }

                long at = 0x100;

                foreach (long[] page in pages)
                {
                    if (page[0] == section.Pages[0][0])
                    {
                        return at + 0x20;
                    }

                    at += page[1];
                }
            }

            return 0;
        }

        private static void Descriptor(Stream map, Section section)
        {
            Long64(map, section.Data.Length);
            Long(map, section.Pages.Count);
            Long(map, section.PageSize);
            Long(map, 1);
            Long(map, section.Compressed ? 2 : 1);
            Long(map, section.Id);
            Long(map, 0);
            var name = new byte[64];
            Encoding.ASCII.GetBytes(section.Name).CopyTo(name, 0);
            map.Write(name, 0, name.Length);

            foreach (long[] page in section.Pages)
            {
                Long(map, (int)page[0]);
                Long(map, (int)page[1]);
                Long64(map, page[2]);
            }
        }

        /// <summary>
        /// A system page: a head of five longs (its type, the size of its data, the size compressed, the kind 2, the sum
        /// seeded by the head), then the data compressed, or stored in one run of literal bytes where
        /// <paramref name="compress"/> is false; returns its size on its 32-byte boundary.
        /// </summary>
        private static int SystemPage(Stream file, uint type, byte[] data, bool compress)
        {
            byte[] packed = compress ? DwgLz77.Compress(data, 0, data.Length) : Stored(data);
            var head = new byte[20];
            Put32(head, 0, type);
            Put32(head, 4, (uint)data.Length);
            Put32(head, 8, (uint)packed.Length);
            Put32(head, 12, 2);
            uint seed = DwgCrc.OfPage(head, 0, 20, 0);
            Put32(head, 16, DwgCrc.OfPage(packed, 0, packed.Length, seed));
            int size = Round32(20 + packed.Length);
            file.Write(head, 0, 20);
            file.Write(packed, 0, packed.Length);
            file.Write(Series, 0, size - 20 - packed.Length);
            return size;
        }

        /// <summary>Data in the compressed form of one run of literal bytes and the closing code: its length does not hang on the bytes.</summary>
        private static byte[] Stored(byte[] data)
        {
            var output = new MemoryStream();

            if (data.Length > 0)
            {
                if (data.Length <= 3)
                {
                    output.WriteByte((byte)(0x11 + data.Length));
                }
                else if (data.Length <= 18)
                {
                    output.WriteByte((byte)(data.Length - 3));
                }
                else
                {
                    output.WriteByte(0);
                    int rest = data.Length - 3 - 0x0F;

                    while (rest > 0xFF)
                    {
                        output.WriteByte(0);
                        rest -= 0xFF;
                    }

                    output.WriteByte((byte)rest);
                }

                output.Write(data, 0, data.Length);
            }

            output.WriteByte(0x11);
            output.WriteByte(0);
            output.WriteByte(0);
            return output.ToArray();
        }

        /// <summary>The bytes <see cref="Stored"/> puts before a run of <paramref name="length"/> literal bytes.</summary>
        private static int LiteralRunLength(int length)
        {
            if (length == 0)
            {
                return 0;
            }

            if (length <= 18)
            {
                return 1;
            }

            return 2 + ((length - 3 - 0x0F - 1) / 0xFF);
        }

        private static int Round32(int size) => (size + 31) & ~31;

        private static byte[] MakeSeries()
        {
            var series = new byte[256];
            uint seed = 1;

            for (int i = 0; i < series.Length; i++)
            {
                seed = (seed * 0x343FD) + 0x269EC3;
                series[i] = (byte)(seed >> 16);
            }

            return series;
        }

        private static void Long(Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void Long64(Stream stream, long value)
        {
            Long(stream, (int)value);
            Long(stream, (int)(value >> 32));
        }

        private static uint Get32(byte[] data, int at) =>
            (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));

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

        private sealed class Section
        {
            public string Name;
            public byte[] Data;
            public int Id;
            public int PageSize;
            public bool Compressed;
            public List<long[]> Pages = new List<long[]>();
        }
    }
}
