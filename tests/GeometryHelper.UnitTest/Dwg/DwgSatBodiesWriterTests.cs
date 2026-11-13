using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A 3DSOLID of SAT text, as a DXF of R2004 or R2010 gives it, written into a DWG of R2018: its SAT turned into the SAB
    /// of R2013 and later (<c>DwgSat.ToSab</c>), named by a GUID of the entity and kept in the data store of the file, as
    /// AutoCAD 2021 does when it saves such a drawing. The keys are AutoCAD's own DXF of a box 30 by 20 by 10, a sphere of
    /// radius 8, a cylinder of radius 5 and a region (acis_AC1018 of R2004, acis_AC1024 of R2010).
    /// </summary>
    public class DwgSatBodiesWriterTests
    {
        private static byte[] Written(byte[] dxf, out IDictionary<string, int> leftOut) =>
            DwgWriter.ToDwg(DxfDatabase.Open(new MemoryStream(dxf, false)).ToText(), "AC1032", out leftOut);

        /// <summary>The lines of the SAT of an entity, as the groups 3 and 1 after its AcDbModelerGeometry hold them, deciphered.</summary>
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

        private static byte[] Sab(byte[] dxf, DwgAnswerKey.Record entity)
        {
            string handle = entity.Groups.First(g => g.Key == 5).Value.Trim();
            DwgAnswerKey.Record data = DwgAnswerKey
                .Parse(dxf)
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

        private static List<DwgAnswerKey.Record> Entities(byte[] dxf, string type) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == "ENTITIES" && r.Type == type).ToList();

        [Theory]
        [InlineData("acis_AC1024", "AC1024")]
        [InlineData("acis_AC1018", "AC1018")]
        public void TheSolidsOfSatText_AreWrittenWithTheirDataInTheStore_AndReadBackAsTheSameSat(
            string key,
            string version
        )
        {
            byte[] source = DwgAnswerKey.Key(key);
            byte[] dwg = Written(source, out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);
            Assert.NotNull(DwgContainer.Read(dwg).DataStore);
            List<DwgAnswerKey.Record> before = Entities(source, "3DSOLID");
            List<DwgAnswerKey.Record> after = Entities(back, "3DSOLID");
            Assert.Equal(3, after.Count);

            for (int i = 0; i < before.Count; i++)
            {
                Assert.Contains(after[i].Groups, g => g.Key == 2 && g.Value.StartsWith("{", StringComparison.Ordinal));
                List<string> lines = Sat(before[i]);
                Assert.Equal(
                    string.Join("\n", lines) + "\n",
                    DwgSat.ToSat(Sab(back, after[i]), AcisFormat.For(version))
                );
            }
        }

        [Fact]
        public void TheRegionOfSatText_IsWrittenAsAcisToo()
        {
            byte[] dwg = Written(DwgAnswerKey.Key("acis_AC1024"), out _);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.Single(Entities(back, "REGION"));
        }

        [Fact]
        public void ASatBodyWithNoGuid_GetsTheSameGuidEveryTime_SoTheFileIsTheSameBytes()
        {
            byte[] source = DwgAnswerKey.Key("acis_AC1018");

            Assert.Equal(Written(source, out _), Written(source, out _));
        }

        [Fact]
        public void ASatOfAnUnknownRecord_IsStillLeftOut()
        {
            // The DXF of R2000 gives ACIS 4.00 text, whose splines (sumsur, the tolerances R2004 adds) the grammars do not place.
            Written(DwgAnswerKey.Key("splines_AC1015"), out IDictionary<string, int> leftOut);

            Assert.Equal(2, leftOut["3DSOLID"]);
        }

        [Fact]
        public void TheSolidAndTheRegionOfR2000_AreWrittenToo_FromTheirACIS4Text()
        {
            byte[] dwg = Written(DwgAnswerKey.Key("solids_AC1015"), out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);
            Assert.Single(Entities(back, "3DSOLID"));
            Assert.Single(Entities(back, "REGION"));
        }
    }
}
