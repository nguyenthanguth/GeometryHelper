using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The visual styles of a DXF of AutoCAD 2004, 2007 and 2010, which keep 28 properties and a record of the 30 R2013
    /// added in the extension dictionary of the style, written into a DWG of R2018 as AutoCAD 2021 writes them when it saves
    /// the same drawing as R2018: the 58 properties in one list, the record and its dictionary taken into the style.
    /// </summary>
    /// <remarks>
    /// matvis_AC10xx was made by probe GhW4b in AutoCAD 2021 (a new drawing with a material and the 25 visual styles of a
    /// new drawing, in each version of DXF); matvis_AC1032 is AutoCAD's own DXF of it as R2018.
    /// </remarks>
    public class DwgOlderVisualStylesTests
    {
        private static byte[] ReadBack(string name)
        {
            DxfDatabase source = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(source, stream);
                return DwgReader.ToDxf(stream.ToArray());
            }
        }

        [Theory]
        [InlineData("matvis_AC1018")]
        [InlineData("matvis_AC1021")]
        [InlineData("matvis_AC1024")]
        public void TheVisualStylesOfAnOlderDxf_WrittenAsDwg_AreThoseAutoCadWritesWhenItSavesTheDrawingAsR2018(
            string name
        )
        {
            byte[] back = ReadBack(name);

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key("matvis_AC1032"), back);

            List<DwgAnswerKey.Record> styles = DwgAnswerKey.Parse(back).Where(r => r.Type == "VISUALSTYLE").ToList();
            Assert.Equal(25, styles.Count);
            Assert.All(styles, style => Assert.Contains(style.Groups, g => g.Key == 70 && g.Value.Trim() == "58"));
            Assert.DoesNotContain("VISUALSTYLE", result.Missing.Keys);

            // The drawing was made in AutoCAD 2021 and saved to DXF of each version: the style Realistic (X:33) keeps its
            // lighting model 3 there only from R2010, as 2 in R2004 and R2007, which the DXF cannot give back.
            Assert.All(result.Differences.Where(d => d.Contains(" VISUALSTYLE ")), d =>
            {
                Assert.NotEqual("matvis_AC1024", name);
                Assert.Contains("VISUALSTYLE X:33:", d);
            });
        }

        [Theory]
        [InlineData("matvis_AC1018")]
        [InlineData("matvis_AC1021")]
        [InlineData("matvis_AC1024")]
        public void TheRoundTripRecordOfAVisualStyle_IsTakenIntoTheStyle_AndNotWrittenBesideIt(string name)
        {
            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(ReadBack(name));

            Assert.DoesNotContain(back, r =>
                r.Type == "XRECORD" && r.Groups.Any(g => g.Value.Trim().StartsWith("RTVSPost2010Prop"))
            );
            Assert.DoesNotContain(back, r => r.Type == "VISUALSTYLE" && r.Groups.Any(g => g.Key == 360));
        }
    }
}
