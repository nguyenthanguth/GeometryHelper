using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The DWG file of AutoCAD 2007 (AC1021) that <see cref="DwgWriter"/> writes: a file header of 0x80 bytes, a header
    /// proper of 0x400 bytes coded by Reed-Solomon, and pages that are coded by Reed-Solomon, compressed, and named by a
    /// page map and a section map. AutoCAD 2021 refused each file of the first attempts with <c>ErrorStatus</c> 434 (an
    /// unsupported file format) before it read one page: it reads the header proper only where its data is compressed,
    /// reads the section map only where it is compressed, wants the size of a page in the section map to be the size of its
    /// data, and compares the parity of the pages of coding 1 with parity made from what its own memory held, which no other
    /// program can write. Each test below holds one of these, or the parts of the files AutoCAD's own files of R2007 (the
    /// test drawings <c>curves_AC1021</c> and <c>props_AC1021</c>) show: the parity of the codes, the hashes and the codings
    /// of the sections, the fixed bytes of the file header.
    /// </summary>
    public class DwgFileOf2007Tests
    {
        /// <summary>The DWG of the DXF the reader makes of one of the test drawings, written as R2007.</summary>
        private static byte[] OursOf(string answer) =>
            DwgWriter.ToDwg(Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer))), "AC1021", out _);

        private static long U64(byte[] data, long at) => BitConverter.ToInt64(data, (int)at);

        /// <summary>The data bytes of <paramref name="blocks"/> blocks of 255 bytes laid one byte after the other, <paramref name="k"/> to a block.</summary>
        private static byte[] Deinterleave(byte[] file, long at, int blocks, int k, long count)
        {
            var data = new byte[count];
            long m = 0;

            for (int j = 0; m < count; j++)
            {
                for (int i = 0; i < k && m < count; i++, m++)
                {
                    data[m] = file[at + j + (i * blocks)];
                }
            }

            return data;
        }

        private sealed class Page
        {
            public long Number;
            public long Offset;
            public long Size8;
            public long USize;
            public long CSize;
            public long Place;
            public long Slot;
        }

        private sealed class Section
        {
            public string Name;
            public long Size;
            public long PageSize;
            public long Hash;
            public long Coding;
            public List<Page> Pages = new List<Page>();
        }

        /// <summary>A file of R2007 taken apart as far as the tests look: the header proper, the maps and the sections.</summary>
        private sealed class Parsed
        {
            public byte[] File;
            public byte[] Coded;
            public byte[] Meta;
            public long PageMapStored;
            public long PageMapSize;
            public long SectionMapStored;
            public long SectionMapSize;
            public List<long[]> Entries = new List<long[]>();
            public List<Section> Sections = new List<Section>();
        }

        private static Parsed Parse(byte[] file)
        {
            var parsed = new Parsed { File = file };
            parsed.Coded = Deinterleave(file, 0x80, 3, 239, 3 * 239);
            int size = BitConverter.ToInt32(parsed.Coded, 0x18);
            parsed.Meta = new byte[0x110];

            if (size > 0)
            {
                Assert.Equal(0x110, DwgLz21.Decompress(parsed.Coded, 0x20, size, parsed.Meta));
            }
            else
            {
                Array.Copy(parsed.Coded, 0x20, parsed.Meta, 0, -size);
            }

            byte[] pageMap = SystemPage(
                file,
                0,
                U64(parsed.Meta, 0x50),
                U64(parsed.Meta, 0x58),
                U64(parsed.Meta, 0x18)
            );
            parsed.PageMapStored = U64(parsed.Meta, 0x50);
            parsed.PageMapSize = U64(parsed.Meta, 0x58);
            long place = 0;
            var places = new Dictionary<long, long[]>();

            for (int at = 0; at + 16 <= pageMap.Length; at += 16)
            {
                parsed.Entries.Add(new[] { U64(pageMap, at), U64(pageMap, at + 8) });
                places[U64(pageMap, at + 8)] = new[] { place, U64(pageMap, at) };
                place += U64(pageMap, at);
            }

            parsed.SectionMapStored = U64(parsed.Meta, 0xB0);
            parsed.SectionMapSize = U64(parsed.Meta, 0xC8);
            byte[] map = SystemPage(
                file,
                places[U64(parsed.Meta, 0xC0)][0],
                parsed.SectionMapStored,
                parsed.SectionMapSize,
                U64(parsed.Meta, 0xD8)
            );
            int p = 0;

            while (p + 64 <= map.Length)
            {
                var section = new Section
                {
                    Size = U64(map, p),
                    PageSize = U64(map, p + 8),
                    Hash = U64(map, p + 24),
                    Coding = U64(map, p + 48),
                };
                long nameLength = U64(map, p + 32);
                long count = U64(map, p + 56);
                p += 64;
                section.Name = Encoding.Unicode.GetString(map, p, (int)nameLength).TrimEnd('\0');
                p += (int)nameLength;

                for (long i = 0; i < count; i++)
                {
                    long[] where = places[U64(map, p + 16)];
                    section.Pages.Add(
                        new Page
                        {
                            Offset = U64(map, p),
                            Size8 = U64(map, p + 8),
                            Number = U64(map, p + 16),
                            USize = U64(map, p + 24),
                            CSize = U64(map, p + 32),
                            Place = where[0],
                            Slot = where[1],
                        }
                    );
                    p += 56;
                }

                if (section.Name.Length > 0)
                {
                    parsed.Sections.Add(section);
                }
            }

            return parsed;
        }

        /// <summary>The data of a system page: its first copy, taken out of its blocks, and decompressed where it is compressed.</summary>
        private static byte[] SystemPage(byte[] file, long place, long stored, long size, long repeat)
        {
            long blocks = ((((stored + 7) & ~7L) * repeat) + 238) / 239;
            byte[] coded = Deinterleave(file, 0x480 + place, (int)blocks, 239, stored);

            if (stored == size)
            {
                return coded;
            }

            var data = new byte[size];
            Assert.Equal((int)size, DwgLz21.Decompress(coded, 0, stored, data));
            return data;
        }

        /// <summary>The bytes a page of coding 4 stores: its blocks of 251 data bytes, taken out of the interleaved blocks.</summary>
        private static byte[] StoredOf(byte[] file, Page page)
        {
            long blocks = (((page.CSize + 7) & ~7L) + 250) / 251;
            return Deinterleave(file, 0x480 + page.Place, (int)blocks, 251, page.CSize);
        }

        /// <summary>The data of a section of coding 4: its pages taken out of their blocks and decompressed where they are compressed, one after the other.</summary>
        private static byte[] DataOf(byte[] file, Parsed parsed, string name)
        {
            var data = new MemoryStream();

            foreach (Page page in parsed.Sections.Single(s => s.Name == name).Pages)
            {
                byte[] stored = StoredOf(file, page);

                if (page.CSize < page.USize)
                {
                    var plain = new byte[page.USize];
                    Assert.Equal((int)page.USize, DwgLz21.Decompress(stored, 0, stored.Length, plain));
                    stored = plain;
                }

                data.Write(stored, 0, stored.Length);
            }

            return data.ToArray();
        }

        [Fact]
        public void TheFreeSpace_IsAutoCadsOfR2007_OfNumbersOf32Bits()
        {
            byte[] answer = DwgAnswerKey.Dwg("curves_AC1021");
            byte[] file = OursOf("curves");

            byte[] was = DataOf(answer, Parse(answer), "AcDb:ObjFreeSpace");
            byte[] now = DataOf(file, Parse(file), "AcDb:ObjFreeSpace");

            // 53 bytes, not the 89 of R2018; the date of the update at 8 to 15 is the drawing's.
            Assert.Equal(53, now.Length);
            Assert.Equal(was.Take(8).ToArray(), now.Take(8).ToArray());
            Assert.Equal(was.Skip(16).ToArray(), now.Skip(16).ToArray());
        }

        /// <summary>The version and the maintenance release each class of a file of R2007 carries, by the name of its DXF record.</summary>
        private static Dictionary<string, string> ReleasesOf(byte[] file)
        {
            byte[] data = DwgContainer.Read(file).Classes;
            var reader = new DwgBitReader(data);
            long bits = (uint)reader.RawLong();
            long end = DwgBitReader.StringStream(data, 32, bits - 1, out DwgBitReader strings);
            reader = new DwgBitReader(data, 32, end);
            reader.BitLong();
            reader.Bit();
            var releases = new Dictionary<string, string>();

            while (reader.Remaining > 0)
            {
                reader.BitShort();
                reader.BitShort();
                strings.TextUnicode();
                strings.TextUnicode();
                string name = Encoding.UTF8.GetString(strings.TextUnicode());
                reader.Bit();
                reader.BitShort();
                reader.BitLong();
                releases[name] = reader.BitLong() + "." + reader.BitLong();
                reader.BitLong();
                reader.BitLong();
            }

            return releases;
        }

        [Fact]
        public void TheClasses_CarryTheVersionAndTheReleaseAutoCadGivesThem()
        {
            Dictionary<string, string> was = ReleasesOf(DwgAnswerKey.Dwg("curves_AC1021"));
            Dictionary<string, string> now = ReleasesOf(OursOf("curves"));

            // 33.174 for the classes AutoCAD 2021 made when it saved the file, and 27.1 for the cell styles.
            Assert.Equal("33.174", now["MLEADERSTYLE"]);
            Assert.Equal("27.1", now["CELLSTYLEMAP"]);

            foreach (KeyValuePair<string, string> one in was)
            {
                Assert.Equal(one.Value, now[one.Key]);
            }
        }

        [Fact]
        public void TheAuxiliaryHeader_NamesTheVersionOfTheFileBetweenTwoOfTheApplication_AsAutoCadDoes()
        {
            byte[] answer = DwgAnswerKey.Dwg("curves_AC1021");
            byte[] file = OursOf("curves");

            byte[] was = DataOf(answer, Parse(answer), "AcDb:AuxHeader");
            byte[] now = DataOf(file, Parse(file), "AcDb:AuxHeader");

            // The shorts 33, 174 of the application, 27, 50 of the file, 33, 174 again, among the first 32 bytes.
            Assert.Equal(was.Length, now.Length);
            Assert.Equal(was.Take(0x20).ToArray(), now.Take(0x20).ToArray());
        }

        [Fact]
        public void TheParityOfTheHeaderProper_IsAutoCadsParity_OfTheCodeOf239()
        {
            byte[] file = DwgAnswerKey.Dwg("curves_AC1021");
            byte[] data = Deinterleave(file, 0x80, 3, 239, 3 * 239);

            byte[] again = DwgReedSolomon.Of239.Interleave(data, 3);

            // The 765 bytes of the three blocks; what follows up to 0x480 is what AutoCAD's memory held.
            Assert.Equal(file.Skip(0x80).Take(765).ToArray(), again);
        }

        [Fact]
        public void TheParityOfAPage_IsAutoCadsParity_OfTheCodeOf251()
        {
            byte[] file = DwgAnswerKey.Dwg("curves_AC1021");
            Parsed parsed = Parse(file);
            Page page = parsed.Sections.Single(s => s.Name == "AcDb:AcDbObjects").Pages[0];
            long blocks = (((page.CSize + 7) & ~7L) + 250) / 251;
            byte[] logical = Deinterleave(file, 0x480 + page.Place, (int)blocks, 251, blocks * 251);

            byte[] again = DwgReedSolomon.Of251.Interleave(logical, (int)blocks);

            Assert.Equal(file.Skip(0x480 + (int)page.Place).Take(again.Length).ToArray(), again);
        }

        [Fact]
        public void TheLastBlockOfAShortData_IsMadeUpWithZeros_AndKeepsItsParity()
        {
            var data = new byte[300];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((i * 7) + 1);
            }

            // Two blocks of 239 data bytes: the second holds the last 61 bytes and 178 of zeros.
            byte[] coded = DwgReedSolomon.Of239.Interleave(data, 2);
            var second = new byte[239];
            Array.Copy(data, 239, second, 0, 61);
            byte[] alone = DwgReedSolomon.Of239.Interleave(second, 1);

            Assert.Equal(510, coded.Length);

            for (int i = 0; i < 255; i++)
            {
                Assert.Equal(alone[i], coded[(i * 2) + 1]);
            }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(5000)]
        [InlineData(70000)]
        public void CompressedData_ComesBackFromTheDecompression(int length)
        {
            // Runs, and texts that come again from far back, as the objects of a drawing do.
            var data = new byte[length];
            var text = Encoding.ASCII.GetBytes("AcDbEntity LAYER 0 CONTINUOUS AcDbLine ");

            for (int i = 0; i < length; i++)
            {
                data[i] = i % 3000 < 1500 ? text[i % text.Length] : (byte)((i * 31) >> 3);
            }

            byte[] packed = DwgLz21.Compress(data);

            Assert.NotNull(packed);
            Assert.True(packed.Length < length, packed.Length + " of " + length);
            var back = new byte[length];
            Assert.Equal(length, DwgLz21.Decompress(packed, 0, packed.Length, back));
            Assert.Equal(data, back);
        }

        [Fact]
        public void DataThatDoesNotShorten_IsNotCompressed()
        {
            var data = new byte[600];
            uint seed = 12345;

            for (int i = 0; i < data.Length; i++)
            {
                seed = (seed * 1103515245) + 12345;
                data[i] = (byte)(seed >> 16);
            }

            Assert.Null(DwgLz21.Compress(data));
            Assert.Null(DwgLz21.Compress(new byte[10]));
        }

        [Fact]
        public void TheFileHeader_HoldsTheBytesAutoCadWritesInItsOwnFiles()
        {
            byte[] answer = DwgAnswerKey.Dwg("curves_AC1021");
            byte[] file = OursOf("curves");

            Assert.Equal("AC1021", Encoding.ASCII.GetString(file, 0, 6));
            Assert.Equal("AC1021", DwgContainer.Read(file).Version);

            // The version bytes, the code page and the constants, but for the place of the preview (0x0D to 0x10), which
            // follows from the pages, the places of the application information, which is not written, and the unused bytes.
            Assert.Equal(answer.Take(0x0D).ToArray(), file.Take(0x0D).ToArray());
            Assert.Equal(answer.Skip(0x11).Take(0x20 - 0x11).ToArray(), file.Skip(0x11).Take(0x20 - 0x11).ToArray());
            Assert.Equal(answer.Skip(0x28).Take(4).ToArray(), file.Skip(0x28).Take(4).ToArray());
            Assert.Equal(answer.Skip(0x34).Take(8).ToArray(), file.Skip(0x34).Take(8).ToArray());
        }

        [Fact]
        public void TheHeaderProper_IsStoredCompressed_ForAutoCadReadsNoOther()
        {
            Parsed parsed = Parse(OursOf("curves"));

            int stored = BitConverter.ToInt32(parsed.Coded, 0x18);

            Assert.InRange(stored, 1, 0x10F);
        }

        [Fact]
        public void TheHeaderProper_NamesTheFileSize_AndIsCopiedAtTheEndOfTheFile()
        {
            byte[] file = OursOf("curves");
            Parsed parsed = Parse(file);

            Assert.Equal(file.Length, U64(parsed.Meta, 0x08));
            Assert.Equal(0x70, U64(parsed.Meta, 0x00));
            Assert.Equal(file.Length - 0x400 - 0x480, U64(parsed.Meta, 0x48));
            Assert.Equal(file.Skip(0x80).Take(0x400).ToArray(), file.Skip(file.Length - 0x400).ToArray());
        }

        [Fact]
        public void ThePageMap_PlacesEveryPage_AndTheMapsAndTheHeaderFillTheFile()
        {
            byte[] file = OursOf("curves");
            Parsed parsed = Parse(file);

            Assert.Equal(U64(parsed.Meta, 0x60), parsed.Entries.Count);
            Assert.Equal(file.Length, 0x480 + parsed.Entries.Sum(e => e[0]) + 0x400);

            // The page map and its copy first, the section map and its copy last, each pair of one size.
            Assert.Equal(U64(parsed.Meta, 0x40), parsed.Entries[0][1]);
            Assert.Equal(U64(parsed.Meta, 0x30), parsed.Entries[1][1]);
            Assert.Equal(parsed.Entries[0][0], parsed.Entries[1][0]);
            Assert.Equal(parsed.Entries[parsed.Entries.Count - 2][0], parsed.Entries[parsed.Entries.Count - 1][0]);
            Assert.Equal(U64(parsed.Meta, 0xC0), parsed.Entries[parsed.Entries.Count - 2][1]);
            Assert.Equal(U64(parsed.Meta, 0xB8), parsed.Entries[parsed.Entries.Count - 1][1]);
        }

        [Fact]
        public void TheSectionMap_IsStoredCompressed_ForAutoCadReadsNoOther()
        {
            Parsed parsed = Parse(OursOf("curves"));

            Assert.True(
                parsed.SectionMapStored < parsed.SectionMapSize,
                parsed.SectionMapStored + " of " + parsed.SectionMapSize
            );
            Assert.True(parsed.PageMapStored < parsed.PageMapSize, parsed.PageMapStored + " of " + parsed.PageMapSize);
        }

        [Fact]
        public void TheSections_AreTheOnesAutoCadWrites_WithItsHashesAndCodings()
        {
            Parsed answer = Parse(DwgAnswerKey.Dwg("curves_AC1021"));
            Parsed ours = Parse(OursOf("curves"));

            // AutoCAD's application information and history are checked against parity of its own memory, so they are left
            // out; there is no picture, so no preview; and no summary information.
            Assert.Equal(
                new[]
                {
                    "AcDb:RevHistory",
                    "AcDb:AcDbObjects",
                    "AcDb:ObjFreeSpace",
                    "AcDb:Template",
                    "AcDb:Handles",
                    "AcDb:Classes",
                    "AcDb:AuxHeader",
                    "AcDb:Header",
                },
                ours.Sections.Select(s => s.Name).ToArray()
            );

            foreach (Section section in ours.Sections)
            {
                Section same = answer.Sections.Single(s => s.Name == section.Name);
                Assert.Equal(same.Hash, section.Hash);
                Assert.Equal(same.Coding, section.Coding);
            }
        }

        [Fact]
        public void APageOfASection_NamesAsItsSizeTheSizeOfItsData_ForAutoCadRefusesAnother()
        {
            Parsed parsed = Parse(OursOf("curves"));

            foreach (Page page in parsed.Sections.SelectMany(s => s.Pages))
            {
                Assert.Equal(page.USize, page.Size8);
            }

            foreach (Section section in parsed.Sections)
            {
                Assert.Equal(section.Size, section.Pages.Sum(p => p.USize));
            }
        }

        [Fact]
        public void EveryPageOfCoding4_HoldsTheReedSolomonParityOfItsData()
        {
            byte[] file = OursOf("curves");
            Parsed parsed = Parse(file);

            foreach (Page page in parsed.Sections.Where(s => s.Coding == 4).SelectMany(s => s.Pages))
            {
                long blocks = (((page.CSize + 7) & ~7L) + 250) / 251;
                byte[] logical = Deinterleave(file, 0x480 + page.Place, (int)blocks, 251, blocks * 251);
                byte[] again = DwgReedSolomon.Of251.Interleave(logical, (int)blocks);

                Assert.Equal(file.Skip(0x480 + (int)page.Place).Take(again.Length).ToArray(), again);
                Assert.True(
                    page.Slot >= again.Length && page.Slot < again.Length + 32,
                    page.Slot + " for " + again.Length
                );
            }
        }

        [Fact]
        public void APageOfObjects_IsCompressed_WhereThatMakesItShorter()
        {
            byte[] file = OursOf("curves");
            Parsed parsed = Parse(file);
            Page page = parsed.Sections.Single(s => s.Name == "AcDb:AcDbObjects").Pages[0];

            Assert.True(page.CSize < page.USize, page.CSize + " of " + page.USize);
            Assert.Equal(
                DwgContainer.Read(file).Objects.Length,
                parsed.Sections.Single(s => s.Name == "AcDb:AcDbObjects").Size
            );
            Assert.Equal(page.CSize, StoredOf(file, page).Length);
        }

        [Fact]
        public void ADrawingWrittenAsR2007_ReadsBackItsEntitiesWithTheirValues()
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(OursOf("curves"), false));

            DxfLine[] lines = database.ModelSpace.OfType<DxfLine>().ToArray();
            Assert.Equal(3, lines.Length);
            Assert.Equal(1.5, lines[1].StartPoint.X, 12);
            Assert.Equal(-2.25, lines[1].StartPoint.Y, 12);
            Assert.Equal(3.125, lines[1].StartPoint.Z, 12);
            DxfCircle circle = database.ModelSpace.OfType<DxfCircle>().First();
            Assert.Equal(10.0, circle.Center.X, 12);
            Assert.Equal(5.0, circle.Radius, 12);
        }

        [Theory]
        [InlineData("curves_AC1021")]
        [InlineData("props_AC1021")]
        public void ADrawingOfAutoCad2007_WrittenAsR2007Again_ReadsBackItsEntities(string answer)
        {
            string was = Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer)));
            string now = Encoding.UTF8.GetString(DwgReader.ToDxf(OursOf(answer)));

            Assert.Equal(Entities(was), Entities(now));
            Assert.NotEqual(string.Empty, Entities(now));
        }

        /// <summary>The ENTITIES section of a DXF.</summary>
        private static string Entities(string text)
        {
            int start = text.IndexOf("ENTITIES", StringComparison.Ordinal);
            int end = text.IndexOf("ENDSEC", start, StringComparison.Ordinal);
            return start < 0 ? string.Empty : text.Substring(start, end - start);
        }

        [Fact]
        public void ADataWithCopiesOfEveryLengthAndDistance_ComesBackFromTheDecompression()
        {
            // 70 000 bytes that repeat nothing, then copies of 3 to 1 000 bytes from 1 to 65 000 back, each after a few new ones.
            var random = new Random(7);
            var data = new List<byte>();
            var pool = new byte[70000];
            random.NextBytes(pool);
            data.AddRange(pool);

            foreach (int distance in new[] { 1, 2, 100, 512, 513, 4000, 4096, 4097, 8192, 8193, 40000, 65000 })
            {
                foreach (int length in new[] { 3, 10, 14, 15, 18, 19, 30, 50, 51, 255, 256, 1000 })
                {
                    data.Add((byte)random.Next(256));
                    data.Add((byte)random.Next(256));
                    int from = data.Count - distance;

                    for (int i = 0; i < length; i++)
                    {
                        data.Add(data[from + i]);
                    }
                }
            }

            byte[] packed = DwgLz21.Compress(data.ToArray());

            Assert.NotNull(packed);
            // The 70 000 new bytes stay as they are, and the 20 940 that copy them are a few hundred bytes of codes.
            Assert.True(packed.Length < pool.Length + 5000, packed.Length + " of " + data.Count);
            var back = new byte[data.Count];
            Assert.Equal(data.Count, DwgLz21.Decompress(packed, 0, packed.Length, back));
            Assert.Equal(data.ToArray(), back);
        }

        /// <summary>A bitmap of Windows of 24 bits and 8 by 4 pixels, as a THUMBNAILIMAGE keeps it.</summary>
        private static byte[] Bitmap()
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(40));
            bytes.AddRange(BitConverter.GetBytes(8));
            bytes.AddRange(BitConverter.GetBytes(4));
            bytes.AddRange(BitConverter.GetBytes((short)1));
            bytes.AddRange(BitConverter.GetBytes((short)24));
            bytes.AddRange(BitConverter.GetBytes(0));
            bytes.AddRange(BitConverter.GetBytes(96));
            bytes.AddRange(new byte[16]);

            for (int i = 0; i < 96; i++)
            {
                bytes.Add((byte)((i * 7) + 3));
            }

            return bytes.ToArray();
        }

        /// <summary>The DXF of the drawing of curves with a THUMBNAILIMAGE of <paramref name="image"/> added before its end.</summary>
        private static string WithThumbnail(byte[] image)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();
            int eof = text.LastIndexOf("EOF", StringComparison.Ordinal);
            int at = text.LastIndexOf("  0", eof, StringComparison.Ordinal);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var section = new StringBuilder();
            section.Append("  0").Append(newline).Append("SECTION").Append(newline);
            section.Append("  2").Append(newline).Append("THUMBNAILIMAGE").Append(newline);
            section.Append(" 90").Append(newline).Append(image.Length).Append(newline);

            for (int i = 0; i < image.Length; i += 127)
            {
                string hex = string.Concat(image.Skip(i).Take(127).Select(b => b.ToString("X2")));
                section.Append("310").Append(newline).Append(hex).Append(newline);
            }

            section.Append("  0").Append(newline).Append("ENDSEC").Append(newline);
            return text.Insert(at, section.ToString());
        }

        [Fact]
        public void ADrawingWithAPicture_HasItsPreviewFirst_NamedByTheFileHeader_AndItsPicturesAtTheirPlaces()
        {
            byte[] image = Bitmap();
            byte[] file = DwgWriter.ToDwg(WithThumbnail(image), "AC1021", out _);
            Parsed parsed = Parse(file);

            Section preview = parsed.Sections.First(s => s.Name == "AcDb:Preview");
            Page page = preview.Pages.Single();
            Assert.Equal("AcDb:Preview", parsed.Sections.OrderBy(s => s.Pages[0].Number).First().Name);
            Assert.Equal(1, preview.Coding);
            Assert.Equal(0x480 + page.Place, BitConverter.ToInt32(file, 0x0D));

            // The place of the pictures in the preview names a place in the file: the image is where it says.
            DxfDatabase back = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(file), false));
            Assert.Equal(image, back.ThumbnailBitmap);
            int at = BitConverter.ToInt32(file, 0x0D);
            Assert.Equal(image, file.Skip(at + 39 + 80).Take(image.Length).ToArray());
        }

        [Fact]
        public void ADrawingWithNoPicture_HasNoPreview_AndTheFileHeaderNamesNone()
        {
            byte[] file = OursOf("curves");

            Assert.DoesNotContain(Parse(file).Sections, s => s.Name == "AcDb:Preview");
            Assert.Equal(0, BitConverter.ToInt32(file, 0x0D));
        }

        [Fact]
        public void ADrawingOfManyEntities_IsCutIntoPagesOfObjects_AndReadsBackWhole()
        {
            var database = new DxfDatabase("AC1032");

            for (int i = 0; i < 6000; i++)
            {
                database.ModelSpace.AppendEntity(
                    new DxfLine(new GeoPoint3(i, i * 0.5, 0), new GeoPoint3(i + 10, (i * 3.7) % 91, 1))
                );
            }

            var stream = new MemoryStream();
            DwgWriter.Write(database, stream, "AC1021");
            byte[] file = stream.ToArray();

            Section objects = Parse(file).Sections.Single(s => s.Name == "AcDb:AcDbObjects");
            Assert.True(objects.Pages.Count >= 3, objects.Pages.Count + " pages");
            Assert.Equal(objects.Size, objects.Pages.Sum(p => p.USize));
            Assert.Equal(6000, DwgReader.Open(new MemoryStream(file, false)).ModelSpace.OfType<DxfLine>().Count());
        }

        [Fact]
        public void TheSameDrawing_GivesTheSameBytesEveryTime()
        {
            Assert.Equal(OursOf("curves"), OursOf("curves"));
        }
    }
}
