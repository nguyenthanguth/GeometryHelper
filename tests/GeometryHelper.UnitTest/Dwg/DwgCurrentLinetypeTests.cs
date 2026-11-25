using System;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The current linetype (<c>$CELTYPE</c>), layer (<c>$CLAYER</c>) and text style (<c>$TEXTSTYLE</c>) of a drawing that
    /// went through DWG: AutoCAD 2021 refuses a DXF where any of them is empty ("Invalid or incomplete DXF input -- drawing
    /// discarded", ErrorStatus 53), and a blank drawing written as DWG and read back gave an empty <c>$CELTYPE</c>.
    /// </summary>
    public class DwgCurrentLinetypeTests
    {
        private static DxfDatabase ThroughDwg(DxfDatabase database, string version)
        {
            var stream = new MemoryStream();
            DwgWriter.Write(database, stream, version);
            stream.Position = 0;
            return DwgReader.Open(stream);
        }

        /// <summary>What the header of the DXF of the drawing says for a variable, as AutoCAD 2021 reads it.</summary>
        private static string HeaderName(DxfDatabase database, string variable, string code)
        {
            string[] lines = database.ToText().Replace("\r\n", "\n").Split('\n');
            int at = Array.IndexOf(lines, variable);

            Assert.True(at > 0, "the header names " + variable);
            Assert.Equal(code, lines[at + 1].Trim());
            return lines[at + 2];
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1024")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        [InlineData("AC1015")]
        public void ABlankDrawingWrittenAsDwgAndReadBack_HasItsCurrentLinetypeNamed(string version)
        {
            DxfDatabase back = ThroughDwg(new DxfDatabase("AC1032"), version);

            Assert.Equal("ByLayer", HeaderName(back, "$CELTYPE", "6"));
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1015")]
        public void TheDwgOfABlankDrawing_HoldsAHandleForItsCurrentLinetype(string version)
        {
            var stream = new MemoryStream();
            DwgWriter.Write(new DxfDatabase("AC1032"), stream, version);

            DwgHeaderVariables header = DwgHeaderVariables.Read(DwgContainer.Read(stream.ToArray()));

            Assert.NotEqual(0UL, Assert.IsType<ulong>(header.Get("CELTYPE")));
        }

        [Theory]
        [InlineData("$CELTYPE", "6", "ByLayer")]
        [InlineData("$CLAYER", "8", "0")]
        [InlineData("$TEXTSTYLE", "7", "Standard")]
        public void ANameTheTablesDoNotHold_IsReadBackAsTheOneEveryDrawingHas_NotAsAnEmptyName(
            string variable,
            string code,
            string expected
        )
        {
            // The DWG holds no handle for a name no record answers to, as one made by another program may hold none.
            string text = new DxfDatabase("AC1032").ToText().Replace("\r\n", "\n");
            int at = text.IndexOf("$ACADVER", StringComparison.Ordinal);
            string named = text.Insert(at, variable + "\n  " + code + "\nNoSuchRecord\n  9\n");
            Assert.Contains("NoSuchRecord", named);

            DxfDatabase back = ThroughDwg(DxfDatabase.Parse(named), "AC1032");

            Assert.Equal(expected, HeaderName(back, variable, code));
        }
    }
}
