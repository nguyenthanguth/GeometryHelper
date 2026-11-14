using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A TEXT or ATTDEF whose alignment point is (0, 0) has none in AutoCAD's own DWG: flag 0x02 of its data flags says the
    /// point is absent and the reader gives (0, 0) back. The writer gave it as two bit doubles with the insertion point as
    /// their default, 132 bits more, and 40 TEXT and ATTDEF objects of AutoCAD's R2018 drawings differed from its bytes.
    /// </summary>
    public class DwgTextAlignmentTests
    {
        /// <summary>
        /// The DXF of the drawing of meshes with its plain ATTDEF (handle DD) centred, its alignment point (0, 0, 0) or, as
        /// DXF writes a point that is not there, left out.
        /// </summary>
        private static string Centred(bool withTheAlignmentPoint)
        {
            List<string> lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("meshes_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            int handle = lines.FindIndex(l => l.Trim() == "DD");
            Assert.Equal("ATTDEF", lines[handle - 2].Trim());
            int subclass = lines.FindIndex(handle, l => l.Trim() == "AcDbAttributeDefinition") - 1;
            Assert.Equal("100", lines[subclass].Trim());
            lines.InsertRange(
                subclass,
                withTheAlignmentPoint
                    ? new[] { " 72", "     1", " 11", "0.0", " 21", "0.0", " 31", "0.0" }
                    : new[] { " 72", "     1" }
            );
            return string.Join("\n", lines);
        }

        private static byte[] ObjectOfTheAttributeDefinition(string text)
        {
            DwgContainer container = DwgContainer.Read(DwgAnswerKey.Dwg("meshes_AC1032"));
            DwgDrawing drawing = DwgDrawing.Parse(text);
            List<string> classes = drawing.Classes.Select(k => k.Of(null).Text(1).Trim()).ToList();
            var written = new HashSet<ulong>(
                drawing.All.Where(r => !DwgObjectWriter.IsOfDxfAlone(r)).Select(r => r.Handle)
            );
            var writer = new DwgObjectWriter(drawing, DwgContainer.NumberOf(container.Version), classes, written);
            return writer.Write(drawing.Find(0xDD));
        }

        [Fact]
        public void AnAlignmentPointAtTheOrigin_IsWrittenAsNoPoint()
        {
            Assert.Equal(ObjectOfTheAttributeDefinition(Centred(false)), ObjectOfTheAttributeDefinition(Centred(true)));
        }
    }
}
