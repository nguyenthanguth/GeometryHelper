using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// An AutoCAD 2004 file keeps the material of an entity as extended data of ACAD (<c>RTMaterial</c> and the handle of the
    /// material), which its format has no place for. AutoCAD takes it into the entity's material when it reads the file and
    /// writes none of it to R2018: its R2018 save of a drawing with such a LINE has the material handle in the entity's common
    /// data and no extended data. The writer passed the extended data on and left the material at ByLayer.
    /// </summary>
    public class DwgRoundTripMaterialTests
    {
        /// <summary>
        /// The DXF of the drawing of materials; its LINE 7F (material 78) with the material as the round-trip data of an R2004
        /// file has it, or as the entity's own group 347.
        /// </summary>
        private static string Line(bool asRoundTripData)
        {
            List<string> lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("matvis_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            int handle = lines.FindIndex(l => l.Trim() == "7F");
            Assert.Equal("LINE", lines[handle - 2].Trim());

            if (asRoundTripData)
            {
                int material = lines.FindIndex(handle, l => l.Trim() == "347");
                Assert.Equal("78", lines[material + 1].Trim());
                lines.RemoveRange(material, 2);
                int end = lines.FindIndex(handle, l => l == "  0");
                lines.InsertRange(end, new[] { "1001", "ACAD", "1000", "RTMaterial", "1005", "78" });
            }

            return string.Join("\n", lines);
        }

        private static byte[] ObjectOfTheLine(string text)
        {
            DwgContainer container = DwgContainer.Read(DwgAnswerKey.Dwg("matvis_AC1032"));
            DwgDrawing drawing = DwgDrawing.Parse(text);
            List<string> classes = drawing.Classes.Select(k => k.Of(null).Text(1).Trim()).ToList();
            var written = new HashSet<ulong>(
                drawing.All.Where(r => !DwgObjectWriter.IsOfDxfAlone(r)).Select(r => r.Handle)
            );
            var writer = new DwgObjectWriter(drawing, DwgContainer.NumberOf(container.Version), classes, written);
            return writer.Write(drawing.Find(0x7F));
        }

        [Fact]
        public void ALineWithTheRoundTripMaterialOfAnR2004File_IsWrittenWithTheMaterialAndNoExtendedData()
        {
            Assert.Equal(ObjectOfTheLine(Line(false)), ObjectOfTheLine(Line(true)));
        }
    }
}
