using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The check sum of a DWG file and the container of R2000: the version at the start, the section locator, and the
    /// sections with their sentinels and check sums.
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
        [InlineData("AC1018", "AutoCAD 2004")]
        [InlineData("AC1032", "AutoCAD 2018")]
        public void AnotherVersion_IsNotSupported_AndTheMessageNamesIt(string version, string release)
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            Encoding.ASCII.GetBytes(version).CopyTo(file, 0);

            NotSupportedException fault = Assert.Throws<NotSupportedException>(() => DwgContainer.Read(file));
            Assert.Contains(version, fault.Message);
            Assert.Contains(release, fault.Message);
        }

        /// <summary>Where a section starts: record <paramref name="section"/> of the locator, nine bytes each from 0x19, its seeker after its number.</summary>
        internal static int SectionStart(byte[] file, int section) => BitConverter.ToInt32(file, 0x19 + (9 * section) + 1);
    }
}
