using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A DWG of AutoCAD 2004 or before holds its texts in the drawing's code page, a letter beyond it as <c>\U+1EDB</c>:
    /// AutoCAD 2021's own R2000 and R2004 files keep "café" as the bytes of ANSI_1252 and the Vietnamese and Japanese letters
    /// as escapes. The writer gave them as UTF-8, which AutoCAD 2021 read as ANSI_1252 letters ("cafÃ©", "Tiáº¿ng"). A DWG of
    /// R2007 and later is UTF-16 and is not touched.
    /// </summary>
    public class DwgPageTextTests
    {
        private const string Words = "Tiếng Việt café 日本語";
        private const string Layer = "Lớp ẩn";

        /// <summary>The DWG of a drawing with the words in an MTEXT and a layer named with letters, written as <paramref name="version"/>.</summary>
        private static byte[] Drawing(string version, string codePage = "ANSI_1252", string words = Words)
        {
            DxfDatabase database = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            database.LayerTable.Add(new DxfLayerTableRecord { Name = Layer });
            database.ModelSpace.AppendEntity(new DxfMText { Contents = words, Layer = Layer });
            string text = database.ToText().Replace("ANSI_1252", codePage);
            return DwgWriter.ToDwg(text, version);
        }

        /// <summary>The text as bytes, one byte for each letter (all of them below 0x100).</summary>
        private static byte[] Latin1(string text) => Encoding.GetEncoding(28591).GetBytes(text);

        private static bool Holds(byte[] bytes, byte[] part)
        {
            for (int at = 0; at + part.Length <= bytes.Length; at++)
            {
                int i = 0;

                while (i < part.Length && bytes[at + i] == part[i])
                {
                    i++;
                }

                if (i == part.Length)
                {
                    return true;
                }
            }

            return false;
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void TheLettersOfATextBeyondTheCodePage_AreEscapes_AndThoseInIt_AreItsBytes(string version)
        {
            byte[] dxf = DwgReader.ToDxf(Drawing(version));

            // "é" is the byte 0xE9 of ANSI_1252 and the rest are escapes, as AutoCAD 2021 keeps them in its own file.
            Assert.True(Holds(dxf, Latin1("Ti\\U+1EBFng Vi\\U+1EC7t café \\U+65E5\\U+672C\\U+8A9E")));
            Assert.True(Holds(dxf, Latin1("L\\U+1EDBp \\U+1EA9n")));
            Assert.False(Holds(dxf, Encoding.UTF8.GetBytes("ế")));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void ThoseTexts_ComeBackAsTheLettersTyped_InEveryVersion(string version)
        {
            DxfDatabase back = DwgReader.Open(new MemoryStream(Drawing(version), false));

            Assert.Equal(Words, back.ModelSpace.OfType<DxfMText>().Single(t => t.Layer == Layer).Contents);
            Assert.Contains(back.LayerTable, layer => layer.Name == Layer);
        }

        [Fact]
        public void ADrawingOfANotherCodePage_HoldsItsOwnLettersAsBytes_AndTheOthersAsEscapes()
        {
            // ANSI_1251 holds the Cyrillic letters as one byte each (0xCF, 0xE8 ...) and has no é.
            byte[] dwg = Drawing("AC1018", "ANSI_1251", "Привет café");
            byte[] dxf = DwgReader.ToDxf(dwg);

            Assert.True(Holds(dxf, Encoding.GetEncoding(1251).GetBytes("Привет caf\\U+00E9")));
            Assert.Equal(
                "Привет café",
                DwgReader
                    .Open(new MemoryStream(dwg, false))
                    .ModelSpace.OfType<DxfMText>()
                    .Single(t => t.Layer == Layer)
                    .Contents
            );
        }
    }
}
