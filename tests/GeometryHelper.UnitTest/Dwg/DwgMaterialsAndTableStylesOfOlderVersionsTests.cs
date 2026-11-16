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
    /// A file of R2000 and R2004 that AutoCAD 2021 writes holds the three materials, the table style with its cell styles and
    /// the dictionaries that name them, as a file of R2018 does; the writer left the materials out before R2007 and the table
    /// styles before R2010, and every dictionary that named one came out empty. A material of those versions stops before the
    /// settings of R2007, a table style keeps its colours in the form of R2004 and its rows after one another, and the cell
    /// styles of the table style are an object of their own (the CELLSTYLEMAP), written from R2007 down.
    /// </summary>
    public class DwgMaterialsAndTableStylesOfOlderVersionsTests
    {
        /// <summary>The DWG of the DXF the reader makes of one of AutoCAD's own files, written as <paramref name="version"/>.</summary>
        private static byte[] OursOf(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer)), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        /// <summary>The bytes of an object of a file of R2000 or R2004: its size, its data and its check sum.</summary>
        private static byte[] ObjectOf(byte[] file, ulong handle)
        {
            DwgContainer container = DwgContainer.Read(file);
            long at = container.ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(container.Objects, at * 8, container.Objects.LongLength * 8);
            long size = reader.ModularShort();
            int length = (int)(((reader.Position - (at * 8)) / 8) + size + 2);
            return container.Objects.Skip((int)at).Take(length).ToArray();
        }

        [Theory]
        [InlineData("text", "AC1015", 0x11UL)]
        [InlineData("text", "AC1015", 0x19UL)]
        [InlineData("text", "AC1015", 0x21UL)]
        [InlineData("text", "AC1015", 0x10UL)]
        [InlineData("props_AC1018", "AC1018", 0x11UL)]
        [InlineData("props_AC1018", "AC1018", 0x19UL)]
        [InlineData("props_AC1018", "AC1018", 0x21UL)]
        [InlineData("props_AC1018", "AC1018", 0x10UL)]
        public void TheMaterialsAndTheirDictionary_AreAutoCADsBytes_InR2000AndR2004(
            string answer,
            string version,
            ulong handle
        )
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg(answer), handle), ObjectOf(OursOf(answer, version), handle));
        }

        [Theory]
        [InlineData("text", "AC1015", 0x65UL)]
        [InlineData("text", "AC1015", 0x88UL)]
        [InlineData("text", "AC1015", 0x89UL)]
        [InlineData("text", "AC1015", 0x64UL)]
        [InlineData("tables", "AC1015", 0x65UL)]
        [InlineData("tables", "AC1015", 0x85UL)]
        [InlineData("tables", "AC1015", 0x86UL)]
        [InlineData("props_AC1018", "AC1018", 0x65UL)]
        [InlineData("props_AC1018", "AC1018", 0x8CUL)]
        [InlineData("props_AC1018", "AC1018", 0x8DUL)]
        [InlineData("props_AC1018", "AC1018", 0x64UL)]
        public void ATableStyleItsCellStylesAndTheirDictionaries_AreAutoCADsBytes_InR2000AndR2004(
            string answer,
            string version,
            ulong handle
        )
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg(answer), handle), ObjectOf(OursOf(answer, version), handle));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ADrawingOfR2018_WrittenToAnOlderVersion_KeepsItsMaterialsAndTableStyles(string version)
        {
            DwgWriter.ToDwg(
                DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("table_AC1032"), false)).ToText(),
                version,
                out IDictionary<string, int> left
            );

            foreach (string kept in new[] { "MATERIAL", "TABLESTYLE", "CELLSTYLEMAP" })
            {
                Assert.DoesNotContain(left.Keys, key => string.Equals(key, kept, StringComparison.Ordinal));
            }

            // The tables are written too (see DwgTablesOfOlderVersionsTests), and so are the visual styles and the view styles
            // (see DwgStylesOfOlderVersionsTests).
            Assert.DoesNotContain("ACAD_TABLE", left.Keys);
            Assert.DoesNotContain("VISUALSTYLE", left.Keys);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ATableStyleAndAMaterialWrittenToAnOlderVersion_ComeBackAsTheyWereWritten(string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("table_AC1032"), false)).ToText();
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, version));
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(back);

            Assert.Equal(
                DwgAnswerKey.Parse(DwgAnswerKey.Key("table_AC1032")).Count(r => r.Type == "MATERIAL"),
                after.Count(r => r.Type == "MATERIAL")
            );
            Assert.Equal(
                DwgAnswerKey.Parse(DwgAnswerKey.Key("table_AC1032")).Count(r => r.Type == "TABLESTYLE"),
                after.Count(r => r.Type == "TABLESTYLE")
            );
        }
    }
}
