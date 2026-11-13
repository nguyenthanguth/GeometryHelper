using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A DXF of R2004 and before holds a letter beyond its code page as <c>\U+1EDB</c>; a DWG of R2007 and later holds the
    /// letter. The DWG written from the text of a drawing of R2004 kept the escape, and AutoCAD 2021 found "Un-normalized
    /// symbol name" twice in the layer "Lớp ẩn" (AUDIT, 2 errors). The text of R2007 and later has no such escapes, so a
    /// <c>\U+</c> typed in a text of that is the text.
    /// </summary>
    public class DwgUnicodeNamesTests
    {
        /// <summary>The letters of a value the answer key took apart byte by byte, which a DXF of R2007 and later holds as UTF-8.</summary>
        private static string Letters(string value) =>
            Encoding.UTF8.GetString(Encoding.GetEncoding(28591).GetBytes(value.Trim()));

        private static string TextOf(string key) =>
            DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(key), false)).ToText();

        /// <summary>The DXF text with a TEXT entity of <paramref name="value"/> at the end of its ENTITIES.</summary>
        private static string WithText(string dxf, string value)
        {
            List<string> lines = dxf.Replace("\r\n", "\n").Split('\n').ToList();
            int entities = lines.FindIndex(l => l.Trim() == "ENTITIES");
            int end = lines.FindIndex(entities, l => l.Trim() == "ENDSEC");
            int line = lines.FindIndex(entities, l => l.Trim() == "LINE");
            string owner = lines[lines.FindIndex(line, l => l.Trim() == "330") + 1];
            lines.InsertRange(
                end - 1,
                new[]
                {
                    "0",
                    "TEXT",
                    "5",
                    "7F00",
                    "330",
                    owner,
                    "100",
                    "AcDbEntity",
                    "8",
                    "0",
                    "100",
                    "AcDbText",
                    "10",
                    "0.0",
                    "20",
                    "0.0",
                    "30",
                    "0.0",
                    "40",
                    "2.5",
                    "1",
                    value,
                    "100",
                    "AcDbText",
                }
            );
            return string.Join("\n", lines);
        }

        [Fact]
        public void ALayerNamedInVietnameseInTheTextOfR2004_IsWrittenWithItsLetters()
        {
            string text = TextOf("props_AC1018");
            Assert.Contains(@"L\U+1EDBp \U+1EA9n", text);

            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")));

            Assert.Contains(back.Where(r => r.Type == "LAYER"), layer =>
                layer.Groups.Any(g => g.Key == 2 && Letters(g.Value) == "Lớp ẩn")
            );
            Assert.DoesNotContain(back, r => r.Groups.Any(g => g.Value.Contains(@"\U+")));
        }

        [Fact]
        public void ATextOfR2004_WithEscapesInIt_IsWrittenWithItsLetters()
        {
            string text = WithText(TextOf("props_AC1018"), @"a\U+1EDBb\U+00E9");

            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")));

            DwgAnswerKey.Record written = back.Single(r => r.Type == "TEXT");
            Assert.Equal("aớbé", Letters(written.Groups.First(g => g.Key == 1).Value));
        }

        [Fact]
        public void ATextOfR2018_WithAnEscapeTypedInIt_KeepsTheEscape()
        {
            string text = WithText(TextOf("props_AC1032"), @"a\U+1EDBb");

            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")));

            DwgAnswerKey.Record written = back.Single(r => r.Type == "TEXT");
            Assert.Equal(@"a\U+1EDBb", written.Groups.First(g => g.Key == 1).Value.Trim());
        }

        [Theory]
        [InlineData(@"a\M+182A0b", "aあb")]
        [InlineData(@"\M+2A440x", "一x")]
        [InlineData(@"\M+5B0A1", "啊")]
        [InlineData(@"\m+182a0 \u+00e9", "あ é")]
        public void ATextOfR2004_WithMultibyteCodesInIt_IsWrittenWithTheirLetters(string typed, string letters)
        {
            // The number after the plus is the code page (1 Shift-JIS, 2 Big5, 3 Wansung, 4 Johab, 5 GB 2312), then two bytes.
            string text = WithText(TextOf("props_AC1018"), typed);

            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")));

            DwgAnswerKey.Record written = back.Single(r => r.Type == "TEXT");
            Assert.Equal(letters, Letters(written.Groups.First(g => g.Key == 1).Value));
        }

        [Fact]
        public void ATextOfR2018_WithAMultibyteCodeTypedInIt_KeepsTheCode()
        {
            string text = WithText(TextOf("props_AC1032"), @"a\M+182A0b");

            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")));

            DwgAnswerKey.Record written = back.Single(r => r.Type == "TEXT");
            Assert.Equal(@"a\M+182A0b", written.Groups.First(g => g.Key == 1).Value.Trim());
        }
    }
}
