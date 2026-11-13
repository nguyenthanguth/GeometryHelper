using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The container of R2007 (AC1021): a file header of 0x400 bytes at 0x80, coded by Reed-Solomon (255, 239) in three
    /// interleaved blocks and compressed, then pages from 0x480 on. As in R2004 a page map places the pages and a section
    /// map gives the pages of each named section, but both are system pages coded the same way, the data pages are coded
    /// by Reed-Solomon (255, 251), the compression is a variant of its own (<see cref="DwgLz21"/>) and the check sums are
    /// CRCs of 64 bits. The parity bytes of the Reed-Solomon code are not used: the data bytes are taken out of the blocks
    /// and the CRCs and sums say whether they are whole.
    /// </content>
    internal sealed partial class DwgContainer
    {
        /// <summary>Where the pages of R2007 start; the offsets of the page map count from here.</summary>
        private const long PagesStart = 0x480;

        /// <summary>The largest factor by which a system page of R2007 may repeat its data; AutoCAD's repeat theirs a few times.</summary>
        private const long MaxRepeat = 1024;

        private static DwgContainer ReadR21(byte[] file, string version)
        {
            if (file.LongLength < PagesStart)
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

            byte[] meta = FileHeader21(file);
            ulong seed = (ulong)U64(meta, 0xF0);
            long budget = Math.Min(int.MaxValue, (file.LongLength * Expansion) + Allowance);

            long pagesMapAt = U64(meta, 0x38);
            byte[] pageMapData = SystemPage21(
                file,
                pagesMapAt,
                U64(meta, 0x50),
                U64(meta, 0x58),
                U64(meta, 0x18),
                (ulong)U64(meta, 0x10),
                (ulong)U64(meta, 0x80),
                seed,
                "page map",
                ref budget
            );
            Dictionary<long, long[]> pages = ReadPageMap21(pageMapData, file.LongLength);

            if (!pages.TryGetValue(U64(meta, 0xC0), out long[] sectionMapPage))
            {
                throw new FormatException("The DWG page map does not place the section map.");
            }

            byte[] sectionMapData = SystemPage21(
                file,
                sectionMapPage[0],
                U64(meta, 0xB0),
                U64(meta, 0xC8),
                U64(meta, 0xD8),
                (ulong)U64(meta, 0xD0),
                (ulong)U64(meta, 0xA8),
                seed,
                "section map",
                ref budget
            );
            Dictionary<string, Descriptor21> sections = ReadSectionMap21(sectionMapData);

            var container = new DwgContainer
            {
                Version = version,
                MaintenanceVersion = maintenance,
                CodePage = codePage >= 0 && codePage < CodePageNames.Length ? CodePageNames[codePage] : "ANSI_1252",
                Measurement = -1,
            };

            container.Header = Section(
                Join21(file, pages, Need21(sections, "AcDb:Header"), seed, ref budget),
                HeaderStart,
                HeaderEnd,
                "header variables",
                false
            );
            container.Classes = Section(
                Join21(file, pages, Need21(sections, "AcDb:Classes"), seed, ref budget),
                ClassesStart,
                ClassesEnd,
                "classes",
                false
            );
            byte[] handles = Join21(file, pages, Need21(sections, "AcDb:Handles"), seed, ref budget);
            container.Objects = Join21(file, pages, Need21(sections, "AcDb:AcDbObjects"), seed, ref budget);
            container.ObjectMap = ReadObjectMap(handles, 0, handles.LongLength, container.Objects.LongLength);

            if (sections.TryGetValue("AcDb:Preview", out Descriptor21 preview))
            {
                try
                {
                    // A preview no program needs to read the drawing: a page of it that is broken is no reason to refuse the file.
                    container.Preview = Join21(file, pages, preview, seed, ref budget);
                }
                catch (FormatException) { }
            }

            if (sections.TryGetValue("AcDb:Template", out Descriptor21 template))
            {
                byte[] data = Join21(file, pages, template, seed, ref budget);

                if (data.Length >= 2)
                {
                    // The description before the value is a count of UTF-16 characters from R2007 on.
                    int length = 2 * (data[0] | (data[1] << 8));

                    if (2 + length + 2 <= data.Length)
                    {
                        container.Measurement = data[2 + length] | (data[3 + length] << 8);
                    }
                }
            }

            return container;
        }

        /// <summary>
        /// The data of the file header of R2007, 0x110 bytes: the 0x400 bytes at 0x80 are three Reed-Solomon blocks,
        /// interleaved, whose data begins with a CRC, a key, the CRC of the compressed data and its size, then the data.
        /// The data is compressed where its size is positive and taken as it is where the size is negative. The three CRCs
        /// are checked: that of the key and the key turned by itself, that of the compressed data, and that of the data.
        /// </summary>
        private static byte[] FileHeader21(byte[] file)
        {
            byte[] coded = Deinterleave(file, 0x80, 3, 239, 3 * 239);
            ulong key = (ulong)U64(coded, 8);
            int turn = (int)(key & 0x1F);
            ulong turned = turn == 0 ? key : (key << turn) | (key >> (64 - turn));
            var check = new byte[16];
            Array.Copy(coded, 8, check, 0, 8);

            for (int i = 0; i < 8; i++)
            {
                check[8 + i] = (byte)(turned >> (8 * i));
            }

            if (DwgCrc.Of64(check, 0, 16, DwgCrc.Seed1(0, 16), false) != (ulong)U64(coded, 0))
            {
                throw new FormatException("The DWG file header has a wrong check sum.");
            }

            int size = (int)U32(coded, 0x18);
            int length = size < 0 ? -size : size;
            var meta = new byte[0x110];

            if (size == 0 || size == int.MinValue || 0x20 + length > coded.Length || (size < 0 && length > 0x110))
            {
                throw new FormatException("The DWG file header is broken: it names more data than it holds.");
            }

            if (DwgCrc.Of64(coded, 0x20, length, DwgCrc.Seed2(0, length), false) != (ulong)U64(coded, 0x10))
            {
                throw new FormatException("The DWG file header has a wrong check sum.");
            }

            if (size > 0)
            {
                if (DwgLz21.Decompress(coded, 0x20, length, meta) != meta.Length)
                {
                    throw new FormatException("The DWG file header decompresses to another size than it names.");
                }
            }
            else
            {
                Array.Copy(coded, 0x20, meta, 0, length);
            }

            ulong crc = (ulong)U64(meta, 0x108);

            for (int i = 0x108; i < 0x110; i++)
            {
                meta[i] = 0;
            }

            if (DwgCrc.Of64(meta, 0, meta.Length, DwgCrc.Seed2(0, meta.Length), false) != crc)
            {
                throw new FormatException("The DWG file header has a wrong check sum.");
            }

            return meta;
        }

        /// <summary>
        /// The first <paramref name="count"/> data bytes of <paramref name="blocks"/> Reed-Solomon blocks of 255 bytes,
        /// interleaved from <paramref name="at"/>: byte i of block j lies at <c>at + i * blocks + j</c>, and the data of
        /// block j are its first <paramref name="k"/> bytes, after those of the blocks before it.
        /// </summary>
        private static byte[] Deinterleave(byte[] file, long at, long blocks, int k, long count)
        {
            if (blocks <= 0 || count < 0 || count > blocks * k || at < 0 || at + (blocks * 255) > file.LongLength)
            {
                throw new FormatException("The DWG file is cut short: a page of it ends past the end of the file.");
            }

            var data = new byte[count];

            long m = 0;

            for (long j = 0; m < count; j++)
            {
                long from = at + j;

                for (int i = 0; i < k && m < count; i++, m++, from += blocks)
                {
                    data[m] = file[from];
                }
            }

            return data;
        }

        /// <summary>
        /// A system page of R2007, the page map or the section map, at <paramref name="offset"/> from the start of the
        /// pages: its data, compressed where the compressed size is less than the size, padded to eight bytes and repeated
        /// <paramref name="repeat"/> times, coded by Reed-Solomon (255, 239) in interleaved blocks. Only the first copy is
        /// read. Its two mirrored CRCs of 64 bits, of the compressed data and of the data, are checked from the file's seed.
        /// </summary>
        private static byte[] SystemPage21(
            byte[] file,
            long offset,
            long compressed,
            long size,
            long repeat,
            ulong crcCompressed,
            ulong crc,
            ulong seed,
            string name,
            ref long budget
        )
        {
            if (
                offset < 0
                || offset > file.LongLength
                || compressed <= 0
                || compressed > file.LongLength
                || size <= 0
                || size > budget
                || compressed > size
                || repeat <= 0
                || repeat > MaxRepeat
            )
            {
                throw new FormatException("The DWG " + name + " is broken.");
            }

            long aligned = (compressed + 7) & ~7L;
            long blocks = ((aligned * repeat) + 238) / 239;
            byte[] coded = Deinterleave(file, PagesStart + offset, blocks, 239, compressed);
            budget -= size;

            if (DwgCrc.Of64(coded, 0, compressed, DwgCrc.Seed1(seed, compressed), true) != crcCompressed)
            {
                throw new FormatException("The DWG " + name + " has a wrong check sum.");
            }

            byte[] data = coded;

            if (compressed < size)
            {
                data = new byte[size];

                if (DwgLz21.Decompress(coded, 0, compressed, data) != size)
                {
                    throw new FormatException("The DWG " + name + " decompresses to another size than it names.");
                }
            }

            if (DwgCrc.Of64(data, 0, size, DwgCrc.Seed1(seed, size), true) != crc)
            {
                throw new FormatException("The DWG " + name + " has a wrong check sum.");
            }

            return data;
        }

        /// <summary>
        /// The page map of R2007: pairs of a size and a number, the pages laid one after another from the start of the
        /// pages; a negative number is a page not in use. Gives the offset and the size of each page by number.
        /// </summary>
        private static Dictionary<long, long[]> ReadPageMap21(byte[] data, long fileLength)
        {
            var pages = new Dictionary<long, long[]>();
            long offset = 0;

            for (int at = 0; at + 16 <= data.Length; at += 16)
            {
                long size = U64(data, at);
                long number = U64(data, at + 8);

                if (size < 0 || size > fileLength)
                {
                    throw new FormatException("The DWG page map is broken.");
                }

                if (number > 0 && PagesStart + offset + size <= fileLength)
                {
                    pages[number] = new[] { offset, size };
                }

                offset += size;
            }

            return pages;
        }

        /// <summary>A section of the section map of R2007: its size, the size of its pages, its coding, and its pages.</summary>
        private sealed class Descriptor21
        {
            public string Name;
            public long Size;
            public long PageSize;
            public long Encrypted;
            public long Encoding;
            public List<Page21> Pages = new List<Page21>();
        }

        /// <summary>A page of a section of R2007: where its data starts in the section, its number, its sizes and its sums.</summary>
        private sealed class Page21
        {
            public long Offset;
            public long Number;
            public long Size;
            public long Compressed;
            public ulong Checksum;
            public ulong Crc;
        }

        private static Dictionary<string, Descriptor21> ReadSectionMap21(byte[] data)
        {
            var sections = new Dictionary<string, Descriptor21>(StringComparer.Ordinal);
            int at = 0;

            while (at + 64 <= data.Length)
            {
                var one = new Descriptor21
                {
                    Size = U64(data, at),
                    PageSize = U64(data, at + 8),
                    Encrypted = U64(data, at + 16),
                    Encoding = U64(data, at + 48),
                };

                long nameLength = U64(data, at + 32);
                long pageCount = U64(data, at + 56);
                at += 64;

                if (nameLength < 0 || nameLength > 512 || pageCount < 0 || at + nameLength > data.Length)
                {
                    throw new FormatException("The DWG section map is broken.");
                }

                one.Name = Encoding.Unicode.GetString(data, at, (int)nameLength & ~1).TrimEnd('\0');
                at += (int)nameLength;

                if (
                    pageCount > (data.Length - at) / 56
                    || one.Size < 0
                    || one.PageSize <= 0
                    || one.PageSize > MaxPageSize
                )
                {
                    throw new FormatException("The DWG section map is broken at the section " + one.Name + ".");
                }

                for (long j = 0; j < pageCount; j++)
                {
                    one.Pages.Add(
                        new Page21
                        {
                            Offset = U64(data, at),
                            Number = U64(data, at + 16),
                            Size = U64(data, at + 24),
                            Compressed = U64(data, at + 32),
                            Checksum = (ulong)U64(data, at + 40),
                            Crc = (ulong)U64(data, at + 48),
                        }
                    );
                    at += 56;
                }

                if (one.Name.Length > 0)
                {
                    sections[one.Name] = one;
                }
            }

            return sections;
        }

        private static Descriptor21 Need21(Dictionary<string, Descriptor21> sections, string name)
        {
            if (!sections.TryGetValue(name, out Descriptor21 found))
            {
                throw new FormatException("The DWG file has no section " + name + ".");
            }

            return found;
        }

        /// <summary>
        /// The bytes of a section of R2007: its pages taken out of their Reed-Solomon blocks (interleaved where the coding
        /// of the section is 4, one after another where it is 1), decompressed where their compressed size is less than
        /// their size, and laid at their offsets; the gaps between them, pages of zeros AutoCAD does not write, stay zero.
        /// Of each page the mirrored CRC of 64 bits of its compressed bytes and the sum of its data are checked.
        /// </summary>
        private static byte[] Join21(
            byte[] file,
            Dictionary<long, long[]> pages,
            Descriptor21 section,
            ulong seed,
            ref long budget
        )
        {
            if (section.Encrypted == 1)
            {
                throw new NotSupportedException(
                    "The DWG section " + section.Name + " is encrypted; such files are not read."
                );
            }

            if (section.Encoding != 1 && section.Encoding != 4)
            {
                throw new FormatException("The DWG section " + section.Name + " has a coding that is not known.");
            }

            long total = section.Size;

            foreach (Page21 page in section.Pages)
            {
                // AutoCAD's pages of R2007 may be larger than the page size of their section (0xF800), by a few KB in the
                // test drawings; a page is held to the size any page may have.
                if (
                    page.Offset < 0
                    || page.Size <= 0
                    || page.Size > MaxPageSize
                    || page.Compressed <= 0
                    || page.Compressed > page.Size
                    || page.Offset > budget
                )
                {
                    throw new FormatException("The DWG section " + section.Name + " has a broken page.");
                }

                total = Math.Max(total, page.Offset + page.Size);
            }

            if (total > budget)
            {
                throw new FormatException(
                    "The DWG section " + section.Name + " names a size beyond what the file can hold."
                );
            }

            budget -= total;
            var data = new byte[total];

            foreach (Page21 page in section.Pages)
            {
                if (!pages.TryGetValue(page.Number, out long[] place))
                {
                    throw new FormatException(
                        "The DWG section " + section.Name + " has a page the page map does not place."
                    );
                }

                long aligned = (page.Compressed + 7) & ~7L;
                long blocks = (aligned + 250) / 251;

                if (blocks * 255 > place[1])
                {
                    throw new FormatException("The DWG section " + section.Name + " has a page larger than its place.");
                }

                byte[] coded;

                if (section.Encoding == 4)
                {
                    coded = Deinterleave(file, PagesStart + place[0], blocks, 251, page.Compressed);
                }
                else
                {
                    // Not interleaved: the data blocks one after another, the parity after them all.
                    if (PagesStart + place[0] + (blocks * 255) > file.LongLength)
                    {
                        throw new FormatException(
                            "The DWG file is cut short: a page of it ends past the end of the file."
                        );
                    }

                    coded = new byte[page.Compressed];
                    Array.Copy(file, PagesStart + place[0], coded, 0, coded.LongLength);
                }

                if (DwgCrc.Of64(coded, 0, coded.LongLength, DwgCrc.Seed1(seed, coded.LongLength), true) != page.Crc)
                {
                    throw new FormatException(
                        "The DWG section " + section.Name + " has a page with a wrong check sum."
                    );
                }

                if (page.Compressed < page.Size)
                {
                    var plain = new byte[page.Size];

                    if (DwgLz21.Decompress(coded, 0, coded.LongLength, plain) != plain.Length)
                    {
                        throw new FormatException(
                            "The DWG section "
                                + section.Name
                                + " has a page that decompresses to another size than it names."
                        );
                    }

                    coded = plain;
                }

                if (DwgCrc.OfPage21(coded, 0, coded.LongLength, 0) != page.Checksum)
                {
                    throw new FormatException(
                        "The DWG section " + section.Name + " has a page with a wrong check sum."
                    );
                }

                Array.Copy(coded, 0, data, page.Offset, coded.LongLength);
            }

            return data;
        }
    }
}
