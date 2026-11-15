using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A 3DSOLID and a REGION in a DWG of R2004, R2007 and R2010, where the entity holds its ACIS data itself as SAB (not empty,
    /// version 2, the bytes, the wireframe of a new solid, from R2007 the count of materials 0 and, of a solid, the handle of its
    /// history) and the file has no data store: they were left out of those versions, which only R2013 and later wrote. The keys
    /// are AutoCAD's own DXF of a box 30 by 20 by 10, a sphere of radius 8, a cylinder of radius 5 and a region (acis_AC1018 of
    /// R2004, acis_AC1024 of R2010); AutoCAD 2021's own DWG of them holds the same bytes but for the date of the SAB, which it
    /// stamps there and leaves out of the DXF, and which the writer leaves empty as it does in R2018.
    /// </summary>
    public class DwgSolidsOfOlderVersionsTests
    {
        private static string TextOf(string key) =>
            DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(key), false)).ToText();

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

        private static List<DwgAnswerKey.Record> Entities(byte[] dxf, string type) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == "ENTITIES" && r.Type == type).ToList();

        [Theory]
        [InlineData("acis_AC1018", "AC1018")]
        [InlineData("acis_AC1024", "AC1024")]
        public void TheSolidsAndTheRegion_AreWrittenIntoTheFileOfTheirOwnVersion_AndReadBackAsAutoCadsSatLineForLine(
            string key,
            string version
        )
        {
            byte[] dwg = DwgWriter.ToDwg(TextOf(key), version, out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);
            Assert.Null(DwgContainer.Read(dwg).DataStore);

            foreach (string type in new[] { "3DSOLID", "REGION" })
            {
                List<DwgAnswerKey.Record> before = Entities(DwgAnswerKey.Key(key), type);
                List<DwgAnswerKey.Record> after = Entities(back, type);

                Assert.Equal(type == "3DSOLID" ? 3 : 1, after.Count);

                for (int i = 0; i < before.Count; i++)
                {
                    Assert.Equal(string.Join("\n", Sat(before[i])), string.Join("\n", Sat(after[i])));
                }
            }
        }

        [Theory]
        [InlineData("acis_AC1024", "AC1021", "21200")]
        [InlineData("acis_AC1024", "AC1018", "20800")]
        [InlineData("acis_AC1018", "AC1024", "21500")]
        public void TheSolidsOfADxfOfAnotherVersion_AreWrittenInTheDataOfTheFilesVersion(
            string key,
            string version,
            string data
        )
        {
            byte[] dwg = DwgWriter.ToDwg(TextOf(key), version, out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> solids = Entities(DwgReader.ToDxf(dwg), "3DSOLID");

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);
            Assert.Equal(3, solids.Count);
            Assert.All(solids, solid => Assert.StartsWith(data + " ", Sat(solid)[0]));
        }

        /// <summary>The bytes of a whole number of 0 as SAB holds it: the tag 0x04 and four bytes.</summary>
        private static readonly byte[] Nought = { 0x04, 0, 0, 0, 0 };

        private static bool Holds(byte[] sab, byte[] tail) =>
            Enumerable.Range(0, sab.Length - tail.Length).Any(at => sab.Skip(at).Take(tail.Length).SequenceEqual(tail));

        [Fact]
        public void TheParametricCurveOfALoftOfR2007_EndsWithOneFlag_AsAutoCadsSabDoes()
        {
            // The lofted solid of AutoCAD 2021's own R2007 file ends its parametric curve (par_int_cur) with the side of the surface
            // as one flag, 0x0A, and its closing brace: the flag of R2010 and later that follows it, a nought, is not in R2007, and the
            // writer gave it, which AutoCAD 2021 refused with a modeling error (75004) on opening.
            List<string> lines = Sat(Entities(DwgAnswerKey.Key("splines_AC1021"), "3DSOLID")[1]);
            var ends = Nought.Concat(Nought).Concat(Nought).Concat(Nought).ToArray();

            byte[] sab = DwgSat.ToSab(lines, AcisFormat.For("AC1021"), string.Empty);

            Assert.NotNull(sab);
            Assert.True(Holds(sab, ends.Concat(new byte[] { 0x0A, 0x10, 0x0B, 0x0B, 0x11 }).ToArray()));
        }

        [Fact]
        public void ALoftedSolidOfR2007_IsWrittenIntoAFileOfR2007_AndReadBackAsTheSameSat()
        {
            byte[] dwg = DwgWriter.ToDwg(TextOf("splines_AC1021"), "AC1021", out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);
            List<DwgAnswerKey.Record> before = Entities(DwgAnswerKey.Key("splines_AC1021"), "3DSOLID");
            List<DwgAnswerKey.Record> after = Entities(back, "3DSOLID");

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.Equal(2, after.Count);

            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(string.Join("\n", Sat(before[i])), string.Join("\n", Sat(after[i])));
            }
        }

        [Fact]
        public void ASolidOfAFileOfR2007_HasNoDataStore_AndItsHistoryHandleIsWritten()
        {
            byte[] dwg = DwgWriter.ToDwg(TextOf("acis_AC1024"), "AC1021", out _);
            List<DwgAnswerKey.Record> solids = Entities(DwgReader.ToDxf(dwg), "3DSOLID");

            Assert.Null(DwgContainer.Read(dwg).DataStore);
            Assert.All(solids, solid => Assert.Contains(solid.Groups, g => g.Key == 100 && g.Value == "AcDb3dSolid"));
        }

        [Fact]
        public void TheSolidsOfR2018_StillGoToTheDataStore_AndNotIntoTheEntity()
        {
            byte[] dwg = DwgWriter.ToDwg(TextOf("acis_AC1024"), "AC1032", out _);

            Assert.NotNull(DwgContainer.Read(dwg).DataStore);
        }

        [Fact]
        public void TheSolidsOfSabOfR2018_AreMadeIntoTheSabOfR2010_WhenTheDrawingGoesDown()
        {
            // The drawing of R2018 with its bodies in the data store, saved as R2010 with the SAT AutoCAD gives it there, written
            // as the SAB of R2010 in the entity.
            byte[] r2018 = DwgWriter.ToDwg(TextOf("acis_AC1024"), "AC1032", out _);
            string text = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(r2018), false)).ToText();

            byte[] dwg = DwgWriter.ToDwg(text, "AC1024", out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> solids = Entities(DwgReader.ToDxf(dwg), "3DSOLID");

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.Null(DwgContainer.Read(dwg).DataStore);
            Assert.Equal(3, solids.Count);
            Assert.All(solids, solid => Assert.StartsWith("21500 ", Sat(solid)[0]));
        }
    }
}
