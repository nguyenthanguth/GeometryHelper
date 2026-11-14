using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// AutoCAD 2021 refused every DWG of R2013 the internal writer made (<c>ErrorStatus</c> 53, a filer error) and opens the
    /// same drawing written with the maintenance release 125 AutoCAD writes for R2013 and a long of 0 after the size of the
    /// header and of the classes, as AutoCAD's own R2013 file has (probed: the 36 drawings of the tests and the samples'
    /// DXF written so open with <c>AUDIT</c> as their originals do). A file of R2010 is opened without them.
    /// </summary>
    public class DwgR2013FileTests
    {
        private static byte[] Written(string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();
            return DwgFileWriter.Write(DwgDrawing.Parse(text, null), version, out _);
        }

        [Fact]
        public void AFileOfR2013_NamesTheMaintenanceReleaseAutoCadWritesForIt()
        {
            Assert.Equal(125, Written("AC1027")[0x0B]);
        }

        [Theory]
        [InlineData("AcDb:Header")]
        [InlineData("AcDb:Classes")]
        public void AFileOfR2013_HasALongOfZeroAfterTheSizeOfItsHeaderAndClasses(string section)
        {
            byte[] bytes = DwgContainer.SectionsOf(Written("AC1027"), out _).Single(s => s.Key == section).Value;

            // The sentinel (16 bytes), the size, and the long that AutoCAD's own file of R2013 has there.
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, bytes.Skip(20).Take(4).ToArray());
        }

        [Fact]
        public void AFileOfR2013_IsReadBackToTheSameDxf()
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            List<DwgAnswerKey.Record> to2010 = DwgAnswerKey.Parse(
                DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1024", out _))
            );
            List<DwgAnswerKey.Record> to2013 = DwgAnswerKey.Parse(
                DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1027", out _))
            );

            Assert.Equal(
                to2010.Where(r => r.Type == "LINE").Select(r => r.Key),
                to2013.Where(r => r.Type == "LINE").Select(r => r.Key)
            );
            Assert.Contains(to2013, r => r.Type == "LINE");
        }
    }
}
