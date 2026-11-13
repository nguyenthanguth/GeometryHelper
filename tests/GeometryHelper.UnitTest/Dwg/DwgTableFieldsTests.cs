using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A table whose cell holds a field, and a cell linked to a data link: the reader refused such a table (AutoCAD gives
    /// the flag of linked data as 3, not the 1 of the specification, and a field list and a linked cell were left out), and
    /// the writer left the fields and links out of the table it wrote. The table of a test drawing is given a field in one
    /// cell and a link in another, in the groups of AutoCAD's DXF, written as a DWG of R2018 and read back.
    /// </summary>
    public class DwgTableFieldsTests
    {
        private static string G(int code, string value) => code.ToString().PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, int value) => G(code, value.ToString());

        private static string Header(string type, string handle, string owner) =>
            G(0, type) + G(5, handle) + G(102, "{ACAD_REACTORS") + G(330, owner) + G(102, "}") + G(330, owner);

        private static string Class(string name, string cpp) =>
            G(0, "CLASS") + G(1, name) + G(2, cpp) + G(3, "ObjectDBX Classes") + G(90, 1152) + G(280, 0) + G(281, 0);

        private static string Field(string handle, string owner) =>
            Header("FIELD", handle, owner)
            + G(100, "AcDbField")
            + G(1, "_text")
            + G(2, "%<\\_FldIdx 0>%")
            + G(90, 0)
            + G(97, 0)
            + G(91, 63)
            + G(92, 0)
            + G(94, 9)
            + G(95, 2)
            + G(96, 0)
            + G(300, "")
            + G(93, 0)
            + G(7, "ACFD_FIELD_VALUE")
            + G(93, 3)
            + G(90, 0)
            + G(91, 0)
            + G(94, 0)
            + G(300, "")
            + G(302, "")
            + G(304, "ACVALUE_END")
            + G(301, "")
            + G(98, 0);

        private static string DataLink(string handle, string owner) =>
            Header("DATALINK", handle, owner)
            + G(100, "AcDbDataLink")
            + G(1, "AcExcel")
            + G(300, "")
            + G(301, "Data Link^JPiping Details^JC:\\data\\mass.xls")
            + G(302, "C:\\data\\mass.xls!Sheet1")
            + G(90, 0)
            + G(91, 1835009)
            + G(92, 1)
            + G(170, 2006)
            + G(171, 12)
            + G(172, 18)
            + G(173, 8)
            + G(174, 11)
            + G(175, 6)
            + G(176, 0)
            + G(177, 1)
            + G(93, 0)
            + G(304, "")
            + G(94, 0)
            + G(360, "0")
            + G(305, "CUSTOMDATA")
            + G(1, "DATAMAP_BEGIN")
            + G(90, 0)
            + G(309, "DATAMAP_END");

        /// <summary>The group pairs of a DXF as its lines, code then value.</summary>
        private static List<string> Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();

        /// <summary>The index of the code line of the group of a code and value, from a place on; -1 where there is none.</summary>
        private static int Find(List<string> lines, int from, int code, string value)
        {
            for (int i = from - (from % 2); i + 1 < lines.Count; i += 2)
            {
                if (lines[i].Trim() == code.ToString() && lines[i + 1].Trim() == value)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The code line of the next group of a code from a place on; -1 where there is none.</summary>
        private static int Next(List<string> lines, int from, int code)
        {
            for (int i = from - (from % 2); i + 1 < lines.Count; i += 2)
            {
                if (lines[i].Trim() == code.ToString())
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The code line of the <paramref name="n"/>th group of a code and value from a place on.</summary>
        private static int Nth(List<string> lines, int from, int code, string value, int n)
        {
            int at = from;

            for (int i = 0; i <= n; i++)
            {
                at = Find(lines, at, code, value);
                Assert.True(at >= 0);
                at += 2;
            }

            return at - 2;
        }

        /// <summary>
        /// The drawing of a table (ttable: four rows of three cells) with the field F5001 in the cell of its second row and
        /// first column, the data link F5002 in the cell of its second row and second column, and both objects.
        /// </summary>
        private static string Drawing()
        {
            string text = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("ttable_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n");
            List<string> lines = Lines(text);

            // The ACAD_TABLE: the cell at index 3 (second row, first column) holds the field and no words of its own.
            int table = Find(lines, 0, 0, "ACAD_TABLE");
            Assert.True(table >= 0);
            int cell = Nth(lines, table, 171, "1", 3);
            int rotation = Next(lines, cell, 145);
            lines.InsertRange(rotation + 2, new[] { "344", "F5001" });
            int value = Next(lines, rotation, 301);
            int flags = Next(lines, value, 93);
            lines[flags + 1] = "1";
            int kind = Next(lines, flags, 90);
            lines[kind + 1] = "4";
            int words = Next(lines, kind, 94);
            Assert.True(words > kind);
            int mid = Next(lines, kind + 2, 1);

            if (mid >= 0 && mid < words)
            {
                lines.RemoveRange(mid, 2);
                words -= 2;
            }

            // The words it showed go too.
            lines[Next(lines, words, 302) + 1] = string.Empty;

            // The TABLECONTENT: the same cell holds the field as its content, the next the link, and the table lists the field.
            int content = Find(lines, 0, 0, "TABLECONTENT");
            Assert.True(content >= 0);
            int fieldCell = Nth(lines, content, 1, "LINKEDTABLEDATACELL_BEGIN", 3);
            int contentAt = Next(lines, Find(lines, fieldCell, 309, "DATAMAP_END"), 302);
            Assert.Equal("CONTENT", lines[contentAt + 1].Trim());
            int begin = Find(lines, contentAt, 1, "CELLCONTENT_BEGIN");
            int end = Find(lines, begin, 309, "CELLCONTENT_END");
            lines.RemoveRange(begin + 2, end - begin - 2);
            lines.InsertRange(begin + 2, new[] { "90", "2", "340", "F5001", "91", "0" });

            int linkCell = Nth(lines, content, 1, "LINKEDTABLEDATACELL_BEGIN", 4);
            int linkAt = Find(lines, linkCell, 309, "DATAMAP_END") + 2;
            Assert.Equal("92", lines[linkAt].Trim());
            lines.RemoveRange(linkAt, 2);
            lines.InsertRange(linkAt, new[] { "92", "1", "340", "F5002", "93", "2", "94", "3", "96", "0" });

            int lastRow = content;

            for (
                int next = Find(lines, content, 309, "TABLEROW_END");
                next >= 0;
                next = Find(lines, next + 2, 309, "TABLEROW_END")
            )
            {
                lastRow = next;
            }

            int fields = Find(lines, lastRow, 92, "0");
            lines.RemoveRange(fields, 2);
            lines.InsertRange(fields, new[] { "92", "1", "360", "F5001" });

            // The classes and objects.
            text = string.Join("\n", lines) + "\n";
            string tableContent = lines[content + 3].Trim();
            int classesEnd = text.IndexOf(
                "  0\nENDSEC\n",
                text.IndexOf("\nCLASSES\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Insert(classesEnd, Class("FIELD", "AcDbField") + Class("DATALINK", "AcDbDataLink"));
            int objectsEnd = text.IndexOf(
                "  0\nENDSEC\n",
                text.IndexOf("\nOBJECTS\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Insert(objectsEnd, Field("F5001", tableContent) + DataLink("F5002", tableContent));
            return Regex.Replace(text, @"(\$HANDSEED\n\s*5\n)[0-9A-Fa-f]+", "${1}FFFF");
        }

        private static string Roundtrip(string text, out DwgAnswerKey.Result result)
        {
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            Assert.DoesNotContain("FIELD", leftOut.Keys);
            Assert.DoesNotContain("DATALINK", leftOut.Keys);
            Assert.DoesNotContain("TABLECONTENT", leftOut.Keys);
            byte[] back = DwgReader.ToDxf(dwg);
            result = DwgAnswerKey.Compare(Encoding.UTF8.GetBytes(text), back);
            return Encoding.UTF8.GetString(back).Replace("\r\n", "\n");
        }

        [Fact]
        public void AFieldInACell_WrittenAsDwg_IsReadBackInTheCellTheContentAndTheListOfFields()
        {
            string back = Roundtrip(Drawing(), out DwgAnswerKey.Result result);

            Assert.DoesNotContain(result.Differences, d => d.Contains(" ACAD_TABLE ") || d.Contains(" TABLECONTENT "));
            Assert.Matches(@"\s344\s+F5001\s", back);
            Assert.Matches(@"CELLCONTENT_BEGIN\s+90\s+2\s+340\s+F5001\s+91\s+0\s", back);
            Assert.Matches(@"TABLEROW_END\s+92\s+1\s+360\s+F5001\s", back);
        }

        [Fact]
        public void ACellLinkedToData_WrittenAsDwg_IsReadBackWithTheLinkItsRowsAndColumns()
        {
            string back = Roundtrip(Drawing(), out DwgAnswerKey.Result result);

            Assert.DoesNotContain(result.Differences, d => d.Contains(" TABLECONTENT "));
            Assert.Matches(@"DATAMAP_END\s+92\s+1\s+340\s+F5002\s+93\s+2\s+94\s+3\s+96\s+0\s+95\s+\d+\s", back);
        }
    }
}
