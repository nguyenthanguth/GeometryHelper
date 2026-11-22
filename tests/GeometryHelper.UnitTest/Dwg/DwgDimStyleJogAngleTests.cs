using System;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The jog angle of a dimension style (DIMJOGANG, group 50) is degrees in DXF and radians in a DWG, as the other angles
    /// are. A style made in code with the default 45 degrees wrote 45 as radians, which AutoCAD 2021 read as 58 degrees and
    /// reported as an override of every style made so.
    /// </summary>
    public class DwgDimStyleJogAngleTests
    {
        private static DxfDatabase RoundTrip(double radians, out string text)
        {
            var database = new DxfDatabase("AC1027");
            var style = new DxfDimStyleTableRecord { Name = "GH" };
            database.DimStyleTable.Add(style);
            style.Dimjogang = radians;

            var stream = new MemoryStream();
            DwgWriter.Write(database, stream);
            DxfDatabase back = DwgReader.Open(new MemoryStream(stream.ToArray(), false));
            text = back.ToText();
            return back;
        }

        [Theory]
        [InlineData(30.0)]
        [InlineData(45.0)]
        [InlineData(90.0)]
        public void TheJogAngleOfAStyleMadeInCode_IsWrittenAsRadians_AndReadBackAsTheSameAngle(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;

            DxfDatabase back = RoundTrip(radians, out string text);

            Assert.Equal(radians, back.DimStyleTable["GH"].Dimjogang, 9);
        }

        [Fact]
        public void TheDefaultJogAngleOf45Degrees_IsLeftOutOfTheDwgAsAutoCadLeavesIt()
        {
            RoundTrip(Math.PI / 4.0, out string text);
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            int at = Array.FindIndex(lines, l => l.Trim() == "GH");
            var codes = new System.Collections.Generic.List<string>();

            for (int i = at + 1; i + 1 < lines.Length && lines[i].Trim() != "0"; i += 2)
            {
                codes.Add(lines[i].Trim());
            }

            Assert.DoesNotContain("50", codes);
        }
    }
}
