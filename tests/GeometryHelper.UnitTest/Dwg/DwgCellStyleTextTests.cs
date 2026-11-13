using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A cell style of a table style's own (id 101 and up) whose content names no text style (a DXF made in code without it)
    /// was written with none, as the rows of a table style were before they were given Standard; AutoCAD's AUDIT cannot
    /// repair a table style with such a cell style. It is given the text style Standard.
    /// </summary>
    public class DwgCellStyleTextTests
    {
        /// <summary>The DXF text of the table drawing tcustom, its cell style GhNote (101) given the text style handle <paramref name="style"/>.</summary>
        private static string WithNoteStyle(string style)
        {
            List<string> lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("tcustom_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            int note = lines.FindIndex(l => l == "GhNote");

            while (lines[note - 1].Trim() != "300")
            {
                note = lines.FindIndex(note + 1, l => l == "GhNote");
            }

            int end = lines.FindLastIndex(note, l => l.Trim() == "CONTENTFORMAT_END");

            Assert.Equal("340", lines[end - 5].Trim());
            lines[end - 4] = style;
            return string.Join("\n", lines);
        }

        private static string TextStyleOfNote(byte[] dxf)
        {
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(dxf);
            DwgAnswerKey.Record map = Assert.Single(records, r =>
                r.Type == "CELLSTYLEMAP" && r.Groups.Any(g => g.Value == "GhNote")
            );
            int note = map.Groups.FindIndex(g => g.Key == 300 && g.Value == "GhNote");
            int end = map.Groups.FindLastIndex(note, g => g.Key == 309 && g.Value == "CONTENTFORMAT_END");
            Assert.Equal(340, map.Groups[end - 2].Key);
            return map.Groups[end - 2].Value.Trim();
        }

        [Fact]
        public void ACustomCellStyleWithNoTextStyle_IsWrittenWithStandard()
        {
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(WithNoteStyle("0"), "AC1032"));
            string standard = DwgAnswerKey
                .Parse(back)
                .Single(r => r.Type == "STYLE" && r.Groups.Any(g => g.Key == 2 && g.Value.Trim() == "Standard"))
                .Groups.First(g => g.Key == 5)
                .Value.Trim();

            Assert.Equal(standard, TextStyleOfNote(back));
        }

        [Fact]
        public void ACustomCellStyleThatNamesItsTextStyle_KeepsIt()
        {
            string key = TextStyleOfNote(DwgAnswerKey.Key("tcustom_AC1032"));
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(WithNoteStyle(key), "AC1032"));

            Assert.Equal(key, TextStyleOfNote(back));
        }
    }
}
