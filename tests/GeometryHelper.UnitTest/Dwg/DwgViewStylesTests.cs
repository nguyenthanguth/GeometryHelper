using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The detail and section view styles every drawing of AutoCAD 2013 and later holds, and the manager of the persistent
    /// names of subentities AutoCAD keeps beside solids: read from AutoCAD's DWG of every version to the DXF AutoCAD wrote
    /// of it, and written into a DWG of R2018 that reads back to the same records.
    /// </summary>
    /// <remarks>
    /// views_AC10xx was made by probe GhViews in AutoCAD 2021: a new drawing with the text style GhId, the linetype GhDash
    /// and the block GhArrow, a detail view style GhDetail and a section view style GhSection, each with every property off
    /// AutoCAD's default (a true colour, a colour by block, lineweights, a hatch of 60 % transparency, three hatch angles).
    /// </remarks>
    public class DwgViewStylesTests
    {
        private static readonly string[] ViewTypes =
        {
            "ACDBDETAILVIEWSTYLE",
            "ACDBSECTIONVIEWSTYLE",
            "ACDBASSOCPERSSUBENTMANAGER",
        };

        private static byte[] Written(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                return stream.ToArray();
            }
        }

        private static DxfDatabase Source(string name) =>
            DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

        private static DwgAnswerKey.Record Single(byte[] dxf, string type, string name) =>
            Assert.Single(DwgAnswerKey.Parse(dxf), r =>
                r.Type == type && r.Groups.Any(g => g.Key == 3 && g.Value == name)
            );

        [Theory]
        [InlineData("views_AC1015")]
        [InlineData("views_AC1018")]
        [InlineData("views_AC1032")]
        [InlineData("curves_AC1018")]
        [InlineData("curves_AC1021")]
        [InlineData("curves_AC1024")]
        [InlineData("curves_AC1027")]
        [InlineData("curves_AC1032")]
        [InlineData("solids_AC1015")]
        [InlineData("solids_AC1032")]
        public void TheViewStyles_ReadFromAutoCadsDwg_AreTheDxfAutoCadWroteOfThem(string drawing)
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgAnswerKey.Key(drawing),
                DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing))
            );

            Assert.Empty(result.Differences);
            Assert.All(ViewTypes, type => Assert.False(result.Missing.ContainsKey(type), type + " is missing"));
        }

        [Theory]
        [InlineData("views_AC1015")]
        [InlineData("views_AC1018")]
        [InlineData("views_AC1032")]
        public void ASectionViewStyleOfItsOwn_IsRead_WithItsHatchAndItsLines(string drawing)
        {
            DwgAnswerKey.Record section = Single(
                DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing)),
                "ACDBSECTIONVIEWSTYLE",
                "section of ours"
            );
            List<string> reals = section.Groups.Where(g => g.Key == 40).Select(g => g.Value).ToList();
            List<string> wholes = section.Groups.Where(g => g.Key == 90).Select(g => g.Value.Trim()).ToList();

            // Identifier 0.33 high, arrows 0.43 with an extension of 0.21, offset 0.57; bend lines 0.37 long, overshooting
            // by 0.11, end lines 0.39; label 0.29 high, 0.68 off; ANSI37 at 2.5, 60 % transparent, at 0.1, 0.2 and 0.3.
            Assert.Equal(
                new[]
                {
                    "0.33",
                    "0.43",
                    "0.21",
                    "0.57",
                    "0.37",
                    "0.11",
                    "0.39",
                    "0.29",
                    "0.68",
                    "2.5",
                    "0.1",
                    "0.2",
                    "0.3",
                },
                reals.Select(r =>
                    double.Parse(r, System.Globalization.CultureInfo.InvariantCulture)
                        .ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                )
            );
            Assert.Contains("33554585", wholes);
            Assert.Contains(section.Groups, g => g.Key == 300 && g.Value == "ANSI37");
            Assert.Contains(section.Groups, g => g.Key == 300 && g.Value == "I, O");
        }

        [Fact]
        public void ADetailViewStyleOfItsOwn_IsRead_WithItsTrueColourAndItsColourByBlock()
        {
            DwgAnswerKey.Record detail = Single(
                DwgReader.ToDxf(DwgAnswerKey.Dwg("views_AC1032")),
                "ACDBDETAILVIEWSTYLE",
                "detail of ours"
            );

            // The identifier in RGB 10, 200, 30 (index 102 the nearest), the label by block, the edge jagged (280 3).
            Assert.Contains(detail.Groups, g => g.Key == 420 && g.Value.Trim() == "706590");
            Assert.Contains(detail.Groups, g => g.Key == 62 && g.Value.Trim() == "0");
            Assert.Equal("3", detail.Groups.Last(g => g.Key == 280).Value.Trim());
            Assert.Contains(detail.Groups, g => g.Key == 300 && g.Value == "GhDetail");
        }

        [Fact]
        public void TheSubentityManagerOfR2013On_IsRead_WithNoIdentifiers_AsAutoCadWritesItsDxf()
        {
            // The file of R2018 holds the identifiers 1 to 3 of the four solids; DXF gives their count as 0 and none.
            DwgAnswerKey.Record manager = Assert.Single(
                DwgAnswerKey.Parse(DwgReader.ToDxf(DwgAnswerKey.Dwg("acis_AC1032"))),
                r => r.Type == "ACDBASSOCPERSSUBENTMANAGER"
            );

            Assert.Equal(
                new[] { "2", "3", "0", "2", "3", "0", "0", "0", "0" },
                manager.Groups.Where(g => g.Key == 90).Select(g => g.Value.Trim())
            );
        }

        [Theory]
        [InlineData("views_AC1032")]
        [InlineData("curves_AC1032")]
        [InlineData("solids_AC1032")]
        [InlineData("acis_AC1032")]
        public void TheViewStylesAndTheSubentityManager_WrittenAsDwg_ReadBackToTheSameRecords(string drawing)
        {
            byte[] dwg = Written(Source(drawing));
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(drawing), DwgReader.ToDxf(dwg));

            Assert.All(ViewTypes, type => Assert.False(result.Missing.ContainsKey(type), type + " is missing"));
            Assert.DoesNotContain(result.Differences, d => d.Contains("VIEWSTYLE") || d.Contains("PERSSUBENT"));
        }

        [Fact]
        public void ANewDrawingOfR2018_WrittenAsDwg_LeavesNothingOut()
        {
            DwgWriter.ToDwg(Source("curves_AC1032").ToText(), "AC1032", out IDictionary<string, int> leftOut);

            Assert.Empty(leftOut);
        }

        [Fact]
        public void TheViewStyles_AreWrittenInTheSameBytes()
        {
            Assert.Equal(Written(Source("views_AC1032")), Written(Source("views_AC1032")));
        }
    }
}
