using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The DWG file of AutoCAD 2000 (AC1015) that <see cref="DwgWriter"/> writes: six sections one after the other and not
    /// pages, the places of the objects in the file, and the objects as AutoCAD 2000 lays them out. AutoCAD 2021 refused the
    /// first files of it (<c>ErrorStatus</c> 53). Each test writes the DXF the reader makes of one of AutoCAD's own files of
    /// R2000 (the test drawings <c>curves</c>, <c>polys</c> and <c>text</c>) and compares an object with the bytes AutoCAD gave
    /// the same object, where the writer made it differently before.
    /// </summary>
    public class DwgFileOf2000Tests
    {
        /// <summary>The DWG of the DXF the reader makes of one of AutoCAD's own files of R2000, written as R2000.</summary>
        private static byte[] OursOf(string answer) =>
            DwgWriter.ToDwg(Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer))), "AC1015", out _);

        /// <summary>The bytes of an object of a file of R2000: its size, its data and its check sum.</summary>
        private static byte[] ObjectOf(byte[] file, ulong handle)
        {
            long at = DwgContainer.Read(file).ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(file, at * 8, file.LongLength * 8);
            long size = reader.ModularShort();
            int length = (int)(((reader.Position - (at * 8)) / 8) + size + 2);
            return file.Skip((int)at).Take(length).ToArray();
        }

        [Fact]
        public void TheFileHeader_NamesAC1015_ItsRelease_AndSixSections()
        {
            byte[] file = OursOf("curves");

            Assert.Equal("AC1015", Encoding.ASCII.GetString(file, 0, 6));
            Assert.Equal(15, file[0x0B]);
            Assert.Equal(6, BitConverter.ToInt32(file, 0x15));
            Assert.Equal("AC1015", DwgContainer.VersionOf(file));

            // The reader checks the sum of the header and its sentinel.
            Assert.Equal("AC1015", DwgContainer.Read(file).Version);
        }

        [Fact]
        public void ADrawingWithNoPicture_HasAPreviewOfOnlyTheHeaderAutoCadWrites()
        {
            byte[] file = OursOf("curves");
            int preview = BitConverter.ToInt32(file, 0x0D);
            int variables = BitConverter.ToInt32(file, 0x19 + 1);

            // The sentinels, the size, the count, one entry and the header of 80 bytes: 126 bytes, as AutoCAD 2021 writes it.
            Assert.Equal(126, variables - preview);
        }

        [Fact]
        public void TheObjectMap_NamesThePlaceOfEachObjectInTheFile()
        {
            byte[] file = OursOf("curves");
            DwgContainer container = DwgContainer.Read(file);

            Assert.True(container.ObjectMap.Count > 150, container.ObjectMap.Count + " objects");

            foreach (KeyValuePair<ulong, long> pair in container.ObjectMap)
            {
                var reader = new DwgBitReader(file, pair.Value * 8, file.LongLength * 8);
                reader.ModularShort();
                reader.BitShort();
                reader.RawLong();
                Assert.Equal(pair.Key, reader.Handle());
            }
        }

        [Fact]
        public void TheHeaderAndTheClasses_HaveNoLongOfZeroAfterTheirSize_AndNoTrailer()
        {
            byte[] file = OursOf("curves");

            foreach (int number in new[] { 0, 1 })
            {
                int at = 0x19 + (9 * number);
                long place = BitConverter.ToInt32(file, at + 1);
                int length = BitConverter.ToInt32(file, at + 5);
                int size = BitConverter.ToInt32(file, (int)place + 16);

                // The sentinel, the size, the data, the check sum and the sentinel.
                Assert.Equal(16 + 4 + size + 2 + 16, length);
            }
        }

        [Fact]
        public void ADrawingWrittenAsR2000_ReadsBackItsEntitiesWithTheirValues()
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
        [InlineData(0x54UL)]
        [InlineData(0x55UL)]
        [InlineData(0x5BUL)]
        [InlineData(0x70UL)]
        public void ATableRecord_IsAutoCadsBytes_WithTheReferencedBitSet(ulong handle)
        {
            // The layer 0, the style STANDARD, the linetype ByBlock and the block record of the model space.
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("curves"), handle), ObjectOf(OursOf("curves"), handle));
        }

        [Fact]
        public void TheStandardDimensionStyle_IsAutoCadsBytes_WithItsBitAfterTheLineweights()
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("curves"), 0x58), ObjectOf(OursOf("curves"), 0x58));
        }

        [Theory]
        [InlineData(0x6FUL)]
        [InlineData(0x73UL)]
        public void ALayout_IsAutoCadsBytes_WithTheHandlesOfItsBaseAndNamedUcs(ulong handle)
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("curves"), handle), ObjectOf(OursOf("curves"), handle));
        }

        /// <summary>What an entity of a file says of its links: the bit that leaves them out and the handles it gives.</summary>
        private static string LinksOf(byte[] file, ulong handle)
        {
            long at = DwgContainer.Read(file).ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(file, at * 8, file.LongLength * 8);
            reader.ModularShort();
            long start = reader.Position;
            reader.BitShort();
            int bits = reader.RawLong();
            Assert.Equal(handle, reader.Handle());

            for (int size = reader.BitShort(); size > 0; size = reader.BitShort())
            {
                reader.Handle();
                reader.Bytes(size);
            }

            Assert.False(reader.Bit(), "the entity has proxy graphics");
            int mode = reader.Bits2();
            int reactors = reader.BitLong();
            bool noLinks = reader.Bit();

            // The handles: the owner where the entity is in neither space, the reactors, the extension dictionary, the links.
            reader.Position = start + bits;

            if (mode == 0)
            {
                reader.Handle(handle);
            }

            for (int i = 0; i < reactors; i++)
            {
                reader.Handle(handle);
            }

            reader.Handle(handle);
            return noLinks ? "none" : reader.Handle(handle).ToString("X") + " " + reader.Handle(handle).ToString("X");
        }

        [Theory]
        [InlineData(0x78UL, "0 79")]
        [InlineData(0x79UL, "none")]
        [InlineData(0x7AUL, "none")]
        public void AnEntityOfTheModelSpace_HasAutoCadsLinks_LeftOutWhereItsNeighboursAreNextToIt(
            ulong handle,
            string links
        )
        {
            // 0x78 is the first entity of the space (none before it, 0x79 after), 0x79 and 0x7A have the handles one before and
            // one after them and leave the links out.
            Assert.Equal(links, LinksOf(DwgAnswerKey.Dwg("curves"), handle));
            Assert.Equal(links, LinksOf(OursOf("curves"), handle));
        }

        [Theory]
        [InlineData(0x7DUL)]
        [InlineData(0x7EUL)]
        [InlineData(0x80UL)]
        public void AVertex_IsAutoCadsBytes_LinkedToTheVerticesAroundItAndNotToTheSeqend(ulong handle)
        {
            // The vertices 0x7D to 0x80 of the first polyline: the last has none after it, as the SEQEND 0x81 is in no list.
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("polys"), handle), ObjectOf(OursOf("polys"), handle));
        }

        [Fact]
        public void ASeqend_IsAutoCadsBytes_WithNoLinksToItsNeighbours()
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("polys"), 0x81), ObjectOf(OursOf("polys"), 0x81));
        }

        [Theory]
        [InlineData(0x86UL)]
        [InlineData(0x84UL)]
        public void AnMtext_IsAutoCadsBytes_WithTheBitAfterItsLineSpacingFactor(ulong handle)
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg("text"), handle), ObjectOf(OursOf("text"), handle));
        }
    }
}
