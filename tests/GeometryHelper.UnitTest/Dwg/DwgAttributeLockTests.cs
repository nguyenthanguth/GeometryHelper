using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// An attribute whose DXF is of R2007 or older has one group 280, the lock of its position, where a DXF of R2010 and
    /// later has two: the version (0) before the tag and the lock after the flags. The lock was written as the version, and
    /// a DWG of an attribute with a locked position was refused by AutoCAD 2021 (ErrorStatus 53).
    /// </summary>
    public class DwgAttributeLockTests
    {
        /// <summary>
        /// The DXF of the drawing of blocks with the version taken out of its first ATTDEF, as a DXF of R2007 has it, and its
        /// lock of position set.
        /// </summary>
        private static string WithOnlyTheLock()
        {
            var lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("blocks_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            int definition = lines.FindIndex(l => l.Trim() == "AcDbAttributeDefinition");
            Assert.True(definition > 0 && lines[definition + 1].Trim() == "280");
            lines.RemoveRange(definition + 1, 2);
            int tag = lines.FindIndex(definition, l => l.Trim() == "2");
            int locked = lines.FindIndex(tag, l => l.Trim() == "280");
            Assert.Equal("0", lines[locked + 1].Trim());
            lines[locked + 1] = "1";
            return string.Join("\n", lines);
        }

        [Fact]
        public void AnAttributeDefinitionOfAnR2007Dxf_WithItsPositionLocked_IsWrittenWithVersionZero()
        {
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(WithOnlyTheLock(), "AC1032"));

            DwgAnswerKey.Record definition = DwgAnswerKey.Parse(back).First(r => r.Type == "ATTDEF");
            string[] values = definition.Groups.Where(g => g.Key == 280).Select(g => g.Value.Trim()).ToArray();
            Assert.Equal(new[] { "0", "1" }, values);
        }
    }
}
