using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The check sums of a DWG file and its containers: of R2000 the version at the start, the section locator, and the
    /// sections with their sentinels and check sums; of R2004 and later the masked file header, the pages with their sums
    /// and their compression; of R2007 the file header and the pages coded by Reed-Solomon, their CRCs and their
    /// compression.
    /// </summary>
    public class DwgContainerTests
    {
        [Fact]
        public void TheCheckSum_OfTheDigitsOneToNine_FromZero_IsThatOfCrc16Arc()
        {
            byte[] digits = Encoding.ASCII.GetBytes("123456789");

            Assert.Equal(0xBB3D, DwgCrc.Of(digits, 0, digits.Length, 0));
        }

        [Fact]
        public void TheCheckSum_OfOneByte_IsTheEntryOfTheTable()
        {
            // The table of the specification starts 0x0000, 0xC0C1, 0xC181, 0x0140.
            Assert.Equal(0xC0C1, DwgCrc.Of(new byte[] { 1 }, 0, 1, 0));
            Assert.Equal(0xC181, DwgCrc.Of(new byte[] { 2 }, 0, 1, 0));
            Assert.Equal(0x0140, DwgCrc.Of(new byte[] { 3 }, 0, 1, 0));
        }

        [Fact]
        public void TheCheckSum_OfDataFollowedByItsOwnCheckSum_IsZero()
        {
            byte[] data = { 0x10, 0x20, 0x30, 0x40, 0x50, 0, 0 };
            int crc = DwgCrc.Of(data, 0, 5, DwgCrc.Seed);
            data[5] = (byte)crc;
            data[6] = (byte)(crc >> 8);

            Assert.Equal(0, DwgCrc.Of(data, 0, 7, DwgCrc.Seed));
        }

        [Fact]
        public void TheCheckSum_CountsOnlyTheBytesAsked()
        {
            byte[] data = { 9, 1, 9 };

            Assert.Equal(0xC0C1, DwgCrc.Of(data, 1, 1, 0));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        [InlineData("AC1.50")]
        public void TheVersion_IsTheFirstSixBytes(string version)
        {
            Assert.Equal(version, DwgContainer.VersionOf(Encoding.ASCII.GetBytes(version + "\0\0\0\0")));
        }

        [Fact]
        public void BytesThatAreNotADwg_HaveNoVersion()
        {
            Assert.Null(DwgContainer.VersionOf(null));
            Assert.Null(DwgContainer.VersionOf(new byte[0]));
            Assert.Null(DwgContainer.VersionOf(Encoding.ASCII.GetBytes("AC10")));
            Assert.Null(DwgContainer.VersionOf(Encoding.ASCII.GetBytes("  0\r\nSECTION")));
            Assert.Null(DwgContainer.VersionOf(new byte[] { (byte)'A', (byte)'C', 0, 1, 2, 3, 4 }));
        }

        [Fact]
        public void TheEmptyDrawing_GivesItsVersion_ItsCodePage_AndItsObjectsByRisingHandle()
        {
            DwgContainer container = DwgContainer.Read(DwgAnswerKey.Dwg("empty"));

            Assert.Equal("AC1015", container.Version);
            Assert.Equal("ANSI_1252", container.CodePage);
            Assert.Equal(0, container.Measurement);
            Assert.NotEmpty(container.Header);
            Assert.NotEmpty(container.Classes);
            Assert.True(container.ObjectMap.Count > 100);
            Assert.Equal(container.ObjectMap.Select(pair => pair.Key).OrderBy(handle => handle), container.ObjectMap.Select(pair => pair.Key));
            Assert.Equal(container.ObjectMap.Count, container.ObjectMap.Select(pair => pair.Key).Distinct().Count());
        }

        [Fact]
        public void AByteChangedInTheFileHeader_IsAWrongCheckSum()
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            file[0x0D] ^= 0x01;

            FormatException fault = Assert.Throws<FormatException>(() => DwgContainer.Read(file));
            Assert.Contains("file header", fault.Message);
            Assert.Contains("check sum", fault.Message);
        }

        [Theory]
        [InlineData(0, "header variables")]
        [InlineData(1, "classes")]
        [InlineData(2, "object map")]
        public void AByteChangedInASection_IsAWrongCheckSumThatNamesTheSection(int section, string name)
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            file[SectionStart(file, section) + (section == 2 ? 5 : 30)] ^= 0x10;

            FormatException fault = Assert.Throws<FormatException>(() => DwgContainer.Read(file));
            Assert.Contains(name, fault.Message);
            Assert.Contains("check sum", fault.Message);
        }

        [Fact]
        public void ASentinelChanged_IsRefused()
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            file[SectionStart(file, 0)] ^= 0xFF;

            FormatException fault = Assert.Throws<FormatException>(() => DwgContainer.Read(file));
            Assert.Contains("sentinel", fault.Message);
        }

        [Theory]
        [InlineData("AC1014", "AutoCAD R14")]
        [InlineData("AC1036", "a release not known")]
        public void AnotherVersion_IsNotSupported_AndTheMessageNamesIt(string version, string release)
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            Encoding.ASCII.GetBytes(version).CopyTo(file, 0);

            NotSupportedException fault = Assert.Throws<NotSupportedException>(() => DwgContainer.Read(file));
            Assert.Contains(version, fault.Message);
            Assert.Contains(release, fault.Message);
        }

        [Fact]
        public void TheCheckSumOf32Bits_OfTheDigitsOneToNine_IsThatOfZip()
        {
            byte[] digits = Encoding.ASCII.GetBytes("123456789");

            Assert.Equal(0xCBF43926u, DwgCrc.Of32(digits, 0, digits.Length, 0));
        }

        [Fact]
        public void TheCheckSumOfAPage_FromOne_IsAdler32()
        {
            // Begun from 1 the sums of a page are Adler-32, whose value for "Wikipedia" is well known.
            byte[] word = Encoding.ASCII.GetBytes("Wikipedia");

            Assert.Equal(0x11E60398u, DwgCrc.OfPage(word, 0, word.Length, 1));
        }

        [Fact]
        public void TheCheckSumOfAPage_TakesItsSumsModuloEveryChunk_SoALongRunMatchesTheSpecification()
        {
            // 0x15B0 bytes of 0xFF and one more: the sums are reduced after the first chunk and again after the second.
            var data = Enumerable.Repeat((byte)0xFF, 0x15B1).ToArray();
            long sum1 = 0;
            long sum2 = 0;

            for (int i = 0; i < data.Length; i++)
            {
                sum1 += 0xFF;
                sum2 += sum1;

                if (i == 0x15AF || i == 0x15B0)
                {
                    sum1 %= 0xFFF1;
                    sum2 %= 0xFFF1;
                }
            }

            Assert.Equal((uint)((sum2 << 16) | sum1), DwgCrc.OfPage(data, 0, data.Length, 0));
        }

        [Fact]
        public void ACompressedPage_OfLiteralsAlone_GivesThem()
        {
            // A literal length of 2 means five bytes; 0x11 ends the page.
            byte[] page = { 0x02, (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', 0x11 };
            var output = new byte[16];

            Assert.Equal(5, DwgLz77.Decompress(page, 0, page.Length, output));
            Assert.Equal("abcde", Encoding.ASCII.GetString(output, 0, 5));
        }

        [Theory]
        [InlineData(0x5C, "abcdabcd")]
        [InlineData(0x50, "abcddddd")]
        public void ACompressedPage_CopiesFromEarlierInTheOutput_AlsoOverlappingWhatItWrites(int code, string expected)
        {
            // Four literals, then four bytes copied from four (0x5C) or one (0x50) back.
            byte[] page = { 0x01, (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)code, 0x00, 0x11 };
            var output = new byte[16];

            Assert.Equal(8, DwgLz77.Decompress(page, 0, page.Length, output));
            Assert.Equal(expected, Encoding.ASCII.GetString(output, 0, 8));
        }

        [Fact]
        public void ACompressedPage_ThatWouldRunPastItsSize_ReachBeforeItsStart_OrHasNoEnd_IsRefused()
        {
            byte[] tooLong = { 0x02, 1, 2, 3, 4, 5, 0x11 };
            byte[] tooFar = { 0x01, 1, 2, 3, 4, 0x5C, 0x40, 0x11 };
            byte[] noEnd = { 0x02, 1, 2, 3, 4, 5 };
            byte[] endlessLength = Enumerable.Repeat((byte)0, 100000).ToArray();

            Assert.Throws<FormatException>(() => DwgLz77.Decompress(tooLong, 0, tooLong.Length, new byte[4]));
            Assert.Throws<FormatException>(() => DwgLz77.Decompress(tooFar, 0, tooFar.Length, new byte[64]));
            Assert.Throws<FormatException>(() => DwgLz77.Decompress(noEnd, 0, noEnd.Length, new byte[64]));
            Assert.Throws<FormatException>(() => DwgLz77.Decompress(endlessLength, 0, endlessLength.Length, new byte[64]));
        }

        [Fact]
        public void CompressedPagesOfNoise_AreRefusedOrDecompressedWithinTheirSize_NeverAnythingElse()
        {
            var random = new Random(31);

            for (int round = 0; round < 3000; round++)
            {
                var page = new byte[1 + random.Next(300)];
                random.NextBytes(page);
                var output = new byte[random.Next(2) == 0 ? 0x7400 : random.Next(64)];

                try
                {
                    int written = DwgLz77.Decompress(page, 0, page.Length, output);
                    Assert.InRange(written, 0, output.Length);
                }
                catch (FormatException)
                {
                }
            }
        }

        [Theory]
        [InlineData("curves_AC1018", "AC1018")]
        [InlineData("curves_AC1024", "AC1024")]
        [InlineData("curves_AC1027", "AC1027")]
        [InlineData("curves_AC1032", "AC1032")]
        public void ADrawingOfR2004AndLater_GivesItsVersion_ItsCodePage_AndItsObjectsByRisingHandle(string drawing, string version)
        {
            byte[] file = DwgAnswerKey.Dwg(drawing);
            DwgContainer container = DwgContainer.Read(file);

            Assert.Equal(version, container.Version);
            Assert.Equal("ANSI_1252", container.CodePage);
            Assert.Equal(0, container.Measurement);
            Assert.NotEmpty(container.Header);
            Assert.NotEmpty(container.Classes);
            Assert.True(container.ObjectMap.Count > 100);
            Assert.Equal(container.ObjectMap.Select(pair => pair.Key).OrderBy(handle => handle), container.ObjectMap.Select(pair => pair.Key));
            Assert.All(container.ObjectMap, pair => Assert.InRange(pair.Value, 1, container.Objects.LongLength - 1));

            // The pages decompress to a few times the file, well inside what the reader allows.
            Assert.InRange(container.Objects.LongLength, file.Length, file.Length * 64L);
        }

        [Theory]
        [InlineData(0x80)]
        [InlineData(0x80 + 0x54)]
        [InlineData(0x80 + 0x68)]
        public void AByteChangedInTheMaskedFileHeaderOfR2004_IsAWrongCheckSum(int at)
        {
            byte[] file = DwgAnswerKey.Dwg("blocks_AC1018");
            file[at] ^= 0x01;

            FormatException fault = Assert.Throws<FormatException>(() => DwgContainer.Read(file));
            Assert.Contains("file header", fault.Message);
        }

        [Fact]
        public void AByteChangedInAPageOfR2010_IsCaughtByACheckSum_OrLiesWhereNothingIsRead()
        {
            // Every page the reader takes carries the sums of its head and of its data; a byte changed in one is refused
            // as a wrong check sum. Bytes of pages the reader does not take, and of the gaps between pages, change nothing.
            byte[] file = DwgAnswerKey.Dwg("props_AC1024");
            int caught = 0;

            for (int at = 0x100; at < file.Length; at += 211)
            {
                byte[] changed = (byte[])file.Clone();
                changed[at] ^= 0x08;

                try
                {
                    DwgContainer.Read(changed);
                }
                catch (FormatException fault)
                {
                    Assert.True(fault.Message.Contains("check sum") || fault.Message.Contains("broken"), fault.Message);
                    caught++;
                }
            }

            Assert.True(caught > 30, "Only " + caught + " changed bytes were caught.");
        }

        [Theory]
        [InlineData("curves_AC1021")]
        [InlineData("props_AC1021")]
        public void TheContainerOfR2007_GivesTheSectionsTheDecodersNeed(string name)
        {
            DwgContainer container = DwgContainer.Read(DwgAnswerKey.Dwg(name));

            Assert.Equal("AC1021", container.Version);
            Assert.Equal("ANSI_1252", container.CodePage);
            Assert.Equal(0, container.Measurement);
            Assert.True(container.Header.Length > 700, "The header variables hold " + container.Header.Length + " bytes.");
            Assert.True(container.ObjectMap.Count > 190, "The object map holds " + container.ObjectMap.Count + " objects.");
            Assert.All(container.ObjectMap, entry => Assert.InRange(entry.Value, 1, container.Objects.LongLength - 1));
        }

        [Fact]
        public void AByteChangedInThePagesOfR2007_IsCaughtByTheirCrcs_OrChangesNothingRead()
        {
            // Of R2007 a page is Reed-Solomon blocks: a byte of data changed is a wrong CRC of 64 bits or a wrong sum; a
            // byte of parity, of padding or of a section the reader does not take changes nothing it gives.
            byte[] file = DwgAnswerKey.Dwg("curves_AC1021");
            DwgContainer whole = DwgContainer.Read(file);
            int caught = 0;
            int same = 0;

            for (int at = 0x80; at < file.Length; at += 97)
            {
                byte[] changed = (byte[])file.Clone();
                changed[at] ^= 0x10;
                DwgContainer read;

                try
                {
                    read = DwgContainer.Read(changed);
                }
                catch (FormatException fault)
                {
                    Assert.True(fault.Message.Contains("check sum") || fault.Message.Contains("broken"), fault.Message);
                    caught++;
                    continue;
                }

                Assert.Equal(whole.Header, read.Header);
                Assert.Equal(whole.Classes, read.Classes);
                Assert.Equal(whole.Objects, read.Objects);
                Assert.Equal(whole.ObjectMap, read.ObjectMap);
                same++;
            }

            Assert.True(caught > 30, "Only " + caught + " changed bytes were caught.");
            Assert.True(same > 30, "Only " + same + " changed bytes fell where nothing is read.");
        }

        [Theory]
        [InlineData(0x80)]
        [InlineData(0x83)]
        [InlineData(0x80 + (3 * 40))]
        [InlineData(0x80 + (3 * 16))]
        [InlineData(0x80 + (3 * 60))]
        public void AByteChangedInTheDataOfTheFileHeaderOfR2007_IsAWrongCheckSum(int at)
        {
            // The three blocks are interleaved: byte i of the data of block j lies at 0x80 + 3 i + j. The bytes changed are
            // those of block 0 at 0 (the CRC of the check sequence), 1, 16 (the CRC of the compressed data), 40 and 60 (the
            // compressed data).
            byte[] file = DwgAnswerKey.Dwg("props_AC1021");
            file[at] ^= 0x01;

            FormatException fault = Assert.Throws<FormatException>(() => DwgContainer.Read(file));
            Assert.Contains("file header", fault.Message);
        }

        [Fact]
        public void TheDecompressionOfR2007_OnNoise_ReturnsOrRefuses_WithinItsOutput()
        {
            var random = new Random(2007);

            for (int round = 0; round < 3000; round++)
            {
                var input = new byte[1 + random.Next(600)];
                random.NextBytes(input);
                var output = new byte[random.Next(4000)];

                try
                {
                    int written = DwgLz21.Decompress(input, 0, input.Length, output);
                    Assert.InRange(written, 0, output.Length);
                }
                catch (FormatException)
                {
                }
            }
        }

        [Fact]
        public void TheDecompressionOfR2007_LaysALiteralRunInItsOwnOrder()
        {
            // A first code of 0x20 takes its length, 3, from the third byte after it; the three bytes then come out
            // reversed, as the specification lays a run of three.
            byte[] input = { 0x20, 0x00, 0x00, 0x03, 0x41, 0x42, 0x43 };
            var output = new byte[3];

            Assert.Equal(3, DwgLz21.Decompress(input, 0, input.Length, output));
            Assert.Equal(new byte[] { 0x43, 0x42, 0x41 }, output);
        }

        /// <summary>Where a section starts: record <paramref name="section"/> of the locator, nine bytes each from 0x19, its seeker after its number.</summary>
        internal static int SectionStart(byte[] file, int section) => BitConverter.ToInt32(file, 0x19 + (9 * section) + 1);
    }
}
