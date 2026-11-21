using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace GeometryHelper.PyStubs.UnitTest
{
    /// <summary>The conversion of documentation comments to docstring text, and the wrapping of it.</summary>
    public class DocTextTests
    {
        [Theory]
        [InlineData("T:GeometryHelper.Geometry.GeoPoint3", "GeoPoint3")]
        [InlineData(
            "M:GeometryHelper.Geometry.GeoPoint3.DistanceTo(GeometryHelper.Geometry.GeoPoint3)",
            "GeoPoint3.DistanceTo"
        )]
        [InlineData("P:GeometryHelper.Geometry.GeoPoint3.X", "GeoPoint3.X")]
        [InlineData(
            "M:GeometryHelper.Geometry.GeoPoint3.#ctor(System.Double,System.Double,System.Double)",
            "GeoPoint3"
        )]
        [InlineData("T:System.Collections.Generic.List{GeometryHelper.Geometry.GeoPoint3}", "List")]
        [InlineData("M:A.B.C.Run``1(System.Collections.Generic.IEnumerable{``0})", "C.Run")]
        [InlineData("T:A.B.Pair`2", "Pair")]
        [InlineData("!:Missing", "Missing")]
        [InlineData("F:A.B.Kind.Round", "Kind.Round")]
        public void ACref_IsShownAsItsTypeOrTheTypeAndMember(string cref, string expected)
        {
            Assert.Equal(expected, DocText.CrefName(cref));
        }

        [Fact]
        public void Paragraphs_CollapseWhiteSpace_AndSplitAtParaTags()
        {
            XElement summary = XElement.Parse(
                "<summary>\n   One   line,\n   two lines.\n   <para>Next <c>paragraph</c>\n   here.</para>\n</summary>"
            );

            Assert.Equal(new[] { "One line, two lines.", "Next paragraph here." }, DocText.Paragraphs(summary));
        }

        [Fact]
        public void ALineThatFitsExactly_IsKeptOnOneLineWithItsQuotes()
        {
            // At an indent of 8 the text and the six quotes may take 108 columns.
            string text = new string('a', 108 - 6);

            List<string> lines = DocText.DocstringLines(new[] { text }, 8);

            Assert.Equal(new[] { "        \"\"\"" + text + "\"\"\"" }, lines);
            Assert.Equal(116, lines[0].Length);
        }

        [Fact]
        public void ALineOneColumnTooLong_IsWrapped_SoNoLinePassesTheWidth()
        {
            string text = string.Join(" ", Enumerable.Repeat("word", 40));

            List<string> lines = DocText.DocstringLines(new[] { text }, 8);

            Assert.True(lines.Count > 1);
            Assert.All(lines, l => Assert.True(l.Length <= DocText.LineWidth, l));
            Assert.StartsWith("        \"\"\"word", lines[0]);
            Assert.EndsWith("word\"\"\"", lines[^1]);
        }
    }
}
