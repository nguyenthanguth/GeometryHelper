using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The marker AcDbEntity told from the marker of an entity's own part however the file pads it: the groups between the
    /// two are the entity's common groups, so a LINE hidden by 60 = 1 after its AcDbEntity reads as hidden, and its own points
    /// are read after AcDbLine. The value is read as its text trimmed, so spaces, a tab, a vertical tab or a no-break space of
    /// ANSI_1252 around the name leave it AcDbEntity, and a name that only begins so is the own part's marker.
    /// </summary>
    public class DxfSubclassMarkerTests
    {
        private static DxfLine Line(byte[] marker)
        {
            Encoding latin = Encoding.GetEncoding(28591);
            var bytes = new MemoryStream();

            void Write(string text)
            {
                byte[] written = latin.GetBytes(text);
                bytes.Write(written, 0, written.Length);
            }

            Write("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n9\n$DWGCODEPAGE\n3\nANSI_1252\n0\nENDSEC\n");
            Write("0\nSECTION\n2\nENTITIES\n0\nLINE\n5\n100\n100\n");
            bytes.Write(marker, 0, marker.Length);
            Write("\n8\n0\n60\n1\n100\nAcDbLine\n10\n0\n20\n0\n30\n0\n11\n10\n21\n4\n31\n0\n0\nENDSEC\n0\nEOF\n");
            bytes.Position = 0;
            return Assert.IsType<DxfLine>(DxfDatabase.Open(bytes).ModelSpace.Cast<DxfEntity>().Single());
        }

        [Theory]
        [InlineData(new byte[] { 0x41, 0x63, 0x44, 0x62, 0x45, 0x6E, 0x74, 0x69, 0x74, 0x79 })]
        [InlineData(new byte[] { 0x20, 0x20, 0x41, 0x63, 0x44, 0x62, 0x45, 0x6E, 0x74, 0x69, 0x74, 0x79, 0x09 })]
        [InlineData(new byte[] { 0x0B, 0x41, 0x63, 0x44, 0x62, 0x45, 0x6E, 0x74, 0x69, 0x74, 0x79 })]
        [InlineData(new byte[] { 0x41, 0x63, 0x44, 0x62, 0x45, 0x6E, 0x74, 0x69, 0x74, 0x79, 0xA0 })]
        public void ALineHiddenAfterItsAcDbEntityMarker_ReadsAsHidden_HoweverTheMarkerIsPadded(byte[] marker)
        {
            DxfLine line = Line(marker);

            Assert.False(line.Visible);
            Assert.Equal(10.0, line.EndPoint.X, 9);
            Assert.Equal(4.0, line.EndPoint.Y, 9);
        }

        [Fact]
        public void AMarkerThatOnlyBeginsAsAcDbEntity_IsTheOwnPartsMarker_SoTheVisibilityAfterItIsNotTheLines()
        {
            DxfLine line = Line(Encoding.ASCII.GetBytes("AcDbEntityX"));

            Assert.True(line.Visible);
            Assert.Equal(10.0, line.EndPoint.X, 9);
        }
    }
}
