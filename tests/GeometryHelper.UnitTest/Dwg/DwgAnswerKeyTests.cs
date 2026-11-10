using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// Every test drawing read from its DWG and compared, record by record, with the DXF AutoCAD wrote of the same
    /// drawing; <see cref="DwgAnswerKey"/> says how the drawings were made and which differences are allowed.
    /// </summary>
    public class DwgAnswerKeyTests : IDisposable
    {
        /// <summary>The types the reader does not read yet, which a comparison may find missing.</summary>
        private static readonly string[] NotReadYet =
        {
            "ACDBDETAILVIEWSTYLE", "ACDBSECTIONVIEWSTYLE", "CELLSTYLEMAP", "HATCH", "LEADER", "MATERIAL", "MLEADERSTYLE",
            "TABLESTYLE", "VISUALSTYLE",
        };

        private readonly List<(GeometryHelperLogLevel Level, string Message)> _written = new List<(GeometryHelperLogLevel Level, string Message)>();

        public DwgAnswerKeyTests()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => _written.Add((level, message));
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            GeometryHelperLog.Enable = true;
        }

        [Theory]
        [InlineData("empty", 150)]
        [InlineData("curves", 150)]
        [InlineData("polys", 150)]
        [InlineData("text", 150)]
        [InlineData("blocks", 150)]
        [InlineData("props", 150)]
        [InlineData("later", 150)]
        public void ADrawing_ReadFromItsDwg_IsTheDxfAutoCadWroteOfIt(string name, int atLeast)
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(name), DwgReader.ToDxf(DwgAnswerKey.Dwg(name)));

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet));
            Assert.True(result.Paired >= atLeast, "Only " + result.Paired + " records were paired.");
            Assert.DoesNotContain(_written, line => line.Level == GeometryHelperLogLevel.Warn);
        }

        [Fact]
        public void TheDrawingOfTheTables_IsTheDxfAutoCadWroteOfIt_ButForTheTextSizeAutoCadChanged()
        {
            // The DWG holds the text size 3.5 the drawing was given; AutoCAD wrote 0.2 after reading it back.
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key("tables"), DwgReader.ToDxf(DwgAnswerKey.Dwg("tables")), "$TEXTSIZE");

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet));
            Assert.True(result.Paired >= 150, "Only " + result.Paired + " records were paired.");
        }

        [Fact]
        public void TheComparison_FindsAChangedNumber_AChangedText_AndAMissingRecord()
        {
            byte[] key = DwgAnswerKey.Key("curves");
            string text = System.Text.Encoding.GetEncoding(28591).GetString(key);
            int line = text.IndexOf("\r\nLINE\r\n", StringComparison.Ordinal);
            int circle = text.IndexOf("\r\nCIRCLE\r\n", StringComparison.Ordinal);
            Assert.True(line > 0 && circle > 0);

            // The end of the line moved from 100 to 100.000001: more than the comparison lets pass.
            int eleven = text.IndexOf("\r\n 11\r\n100.0\r\n", line, StringComparison.Ordinal);
            Assert.True(eleven > 0);
            string moved = text.Substring(0, eleven) + "\r\n 11\r\n100.000001\r\n" + text.Substring(eleven + "\r\n 11\r\n100.0\r\n".Length);

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(key, System.Text.Encoding.GetEncoding(28591).GetBytes(moved));

            Assert.Single(result.Differences);
            Assert.Contains("LINE", result.Differences[0]);

            // The circle taken out of the entities: from its 0 group to the next.
            int next = text.IndexOf("\r\n  0\r\n", circle + 4, StringComparison.Ordinal);
            int start = text.LastIndexOf("\r\n  0", circle, StringComparison.Ordinal);
            string without = text.Substring(0, start) + text.Substring(next);

            result = DwgAnswerKey.Compare(key, System.Text.Encoding.GetEncoding(28591).GetBytes(without));

            Assert.Equal(1, result.Missing["CIRCLE"]);
        }

        [Fact]
        public void TheTypesNotReadYet_AreCountedInOneDebugLine_AndTheCallInOneInfoLine()
        {
            DwgReader.ToDxf(DwgAnswerKey.Dwg("later"));

            string skipped = Assert.Single(_written, line => line.Level == GeometryHelperLogLevel.Debug && line.Message.StartsWith("DwgReader:", StringComparison.Ordinal)).Message;
            Assert.Contains("HATCH 1", skipped);
            Assert.Contains("LEADER 1", skipped);
            Assert.Contains("MATERIAL 3", skipped);

            string summary = Assert.Single(_written, line => line.Level == GeometryHelperLogLevel.Info && line.Message.StartsWith("DwgReader:", StringComparison.Ordinal)).Message;
            Assert.Contains("AutoCAD 2000", summary);
            Assert.Contains("0 broken", summary);
        }
    }
}
