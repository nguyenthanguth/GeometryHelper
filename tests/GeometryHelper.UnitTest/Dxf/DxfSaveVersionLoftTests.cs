using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A solid lofted through a circle, an ellipse and a circle (the second solid of AutoCAD 2021's drawing "splines") going from
    /// R2018 to R2000, R2004 and R2007. Its surface is a procedural one (<c>loft_spl_sur</c>) from R2010; the SAT of R2004 turned
    /// out of the SAB held that record under the head of R2004, which AutoCAD 2021 reads as a solid of no volume (a lofted solid
    /// of a circle of 10, one of 5 and one of 8, volume 7 889.8, came out 0 in R2004 and R2007) where R2000 and R2010 were read
    /// with the volume. AutoCAD writes R2004 and R2007 another form (an exact surface of the nubs with tolerant edges) that no
    /// grammar of ours gives: the body goes as the text of R2010, as the bodies R2000 cannot hold do.
    /// </summary>
    public class DxfSaveVersionLoftTests
    {
        private static List<DwgAnswerKey.Record> Solids(string key) =>
            DwgAnswerKey
                .Parse(DwgAnswerKey.Key(key))
                .Where(r => r.Section == "ENTITIES" && r.Type == "3DSOLID")
                .ToList();

        private static List<string> Sat(DwgAnswerKey.Record entity)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in entity.Groups)
            {
                if (group.Key == 100)
                {
                    inside = group.Value == "AcDbModelerGeometry";
                }
                else if (inside && (group.Key == 3 || group.Key == 1))
                {
                    line.Append(group.Value);

                    if (group.Key == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            return lines;
        }

        private static byte[] Sab(DwgAnswerKey.Record entity)
        {
            string handle = entity.Groups.First(g => g.Key == 5).Value.Trim();
            DwgAnswerKey.Record data = DwgAnswerKey
                .Parse(DwgAnswerKey.Key("splines_AC1032"))
                .Single(r =>
                    r.Section == "ACDSDATA"
                    && r.Type == "ACDSRECORD"
                    && r.Groups.Any(g => g.Key == 320 && g.Value.Trim() == handle)
                );
            string hex = string.Concat(data.Groups.Where(g => g.Key == 310).Select(g => g.Value.Trim()));
            int size = int.Parse(data.Groups.First(g => g.Key == 94).Value.Trim(), CultureInfo.InvariantCulture);
            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        [Fact]
        public void TheSabOfALoft_IsRefusedAsTheSatOfR2004AndR2007_ButNotOfR2010()
        {
            byte[] sab = Sab(Solids("splines_AC1032")[1]);

            Assert.Null(DwgSat.ToSat(sab, AcisFormat.For("AC1018")));
            Assert.Null(DwgSat.ToSat(sab, AcisFormat.For("AC1021")));
            Assert.NotNull(DwgSat.ToSat(sab, AcisFormat.For("AC1024")));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ALoftedSolidOfR2018_GoingToAnOlderVersion_IsTheSatOfR2010_WhichAutoCadReadsWithItsVolume(
            string version
        )
        {
            DxfDatabase database = DxfDatabase.Parse(
                Encoding.GetEncoding(28591).GetString(DwgAnswerKey.Key("splines_AC1032"))
            );

            using (var stream = new MemoryStream())
            {
                IReadOnlyList<string> said = database.Save(stream, version);
                List<DwgAnswerKey.Record> solids = DwgAnswerKey
                    .Parse(stream.ToArray())
                    .Where(r => r.Section == "ENTITIES" && r.Type == "3DSOLID")
                    .ToList();

                Assert.Empty(said);
                Assert.Equal(2, solids.Count);

                // AutoCAD's own SAT of the same solid in R2010, but for the date in the second line of its head.
                List<string> loft = Sat(solids[1]);
                List<string> key = Sat(Solids("splines_AC1024")[1]);

                Assert.Contains(loft, l => l.IndexOf("loft_spl_sur", StringComparison.Ordinal) >= 0);
                Assert.Equal(key.Where((l, i) => i != 1), loft.Where((l, i) => i != 1));
            }
        }
    }
}
