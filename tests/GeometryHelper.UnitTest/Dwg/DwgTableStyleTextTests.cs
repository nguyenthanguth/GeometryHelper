using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A table style whose rows name no text style (one made in code without it: group 7 is missing from its data, title and
    /// header rows, as AutoCAD 2021 writes it of such a style, and as its own save of the drawing gives it back) was written
    /// with no text style, and AutoCAD finds <c>AcDbTableStyle(7B) was not repaired</c> in it, three times in each of two
    /// passes of AUDIT (6 errors). The rows are given the text style Standard, as the styles AutoCAD makes itself have.
    /// </summary>
    public class DwgTableStyleTextTests
    {
        /// <summary>The text styles named in the rows of the table style of <paramref name="handle"/> in the DXF of a DWG.</summary>
        private static string[] TextStylesOf(byte[] dxf, string handle) =>
            DwgAnswerKey
                .Parse(dxf)
                .Single(r => r.Type == "TABLESTYLE" && r.Groups.Any(g => g.Key == 5 && g.Value.Trim() == handle))
                .Groups.Where(g => g.Key == 7)
                .Select(g => g.Value.Trim())
                .ToArray();

        [Theory]
        [InlineData("table_AC1015")]
        [InlineData("table_AC1018")]
        [InlineData("table_AC1021")]
        [InlineData("table_AC1032")]
        public void ATableStyleWithNoTextStyleInItsRows_IsWrittenWithStandard(string key)
        {
            string text = DxfDatabase.Open(new System.IO.MemoryStream(DwgAnswerKey.Key(key), false)).ToText();

            Assert.Empty(TextStylesOf(DwgAnswerKey.Key(key), "7B"));

            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(new[] { "Standard", "Standard", "Standard" }, TextStylesOf(back, "7B"));
        }

        [Fact]
        public void ATableStyleThatNamesItsTextStyle_KeepsIt()
        {
            string text = DxfDatabase
                .Open(new System.IO.MemoryStream(DwgAnswerKey.Key("table_AC1032"), false))
                .ToText();

            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(new[] { "Standard", "Standard", "Standard" }, TextStylesOf(back, "65"));
        }
    }
}
