using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A record of the DXF that has no handle cannot be named by a DWG; it is left out and counted by type, with the others
    /// the writer leaves out, and no longer vanishes without a word.
    /// </summary>
    public class DwgWriterNoHandleTests
    {
        /// <summary>The DXF of the drawing of curves with the handle (group 5) taken off the first LINE and the first CIRCLE.</summary>
        private static string WithoutHandles()
        {
            var lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();

            foreach (string type in new[] { "LINE", "CIRCLE" })
            {
                int at = lines.FindIndex(l => l.Trim() == type);
                Assert.True(at > 0 && lines[at - 1].Trim() == "0", type + " found");
                Assert.Equal("5", lines[at + 1].Trim());
                lines.RemoveRange(at + 1, 2);
            }

            return string.Join("\n", lines);
        }

        [Fact]
        public void ARecordWithNoHandle_IsLeftOut_AndCountedByType()
        {
            DwgWriter.ToDwg(WithoutHandles(), "AC1032", out IDictionary<string, int> leftOut);

            Assert.Equal(1, leftOut["LINE"]);
            Assert.Equal(1, leftOut["CIRCLE"]);
        }

        [Fact]
        public void TheRecordsOfAFileWithAllItsHandles_AreNoneLeftOutForTheirHandle()
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();
            DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);

            Assert.DoesNotContain("LINE", leftOut.Keys);
            Assert.DoesNotContain("CIRCLE", leftOut.Keys);
            Assert.DoesNotContain("CLASS", leftOut.Keys);
            Assert.DoesNotContain("ENDTAB", leftOut.Keys);
        }
    }
}
