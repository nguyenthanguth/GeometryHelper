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
    /// A 3DSOLID, a REGION and a BODY in a DWG of R2000, which left them out: the entity holds its ACIS data as the SAT text of
    /// ACIS 4.00 in blocks (the flag of not empty, a bit AutoCAD 2000 sets, the version 1, each block of at most 4 096 bytes with
    /// its size ahead of it and a 0 after the last, then the wireframe of a new solid). AutoCAD 2021's own DWG of R2000 of a box
    /// 30 by 20 by 10 and a region (<c>solids_AC1015</c>) and of two spline solids (<c>splines_AC1015</c>, the second with 9 836
    /// bytes of text in blocks of 4 096, 4 096 and 1 644) are the key: read, and written again, each entity is the very bytes of
    /// AutoCAD's.
    /// </summary>
    public class DwgSolidsOfR2000Tests
    {
        private static string Handle(DwgAnswerKey.Record record) => record.Key.Substring(record.Key.IndexOf(':') + 1);

        private static bool IsBody(DwgAnswerKey.Record record) =>
            record.Section == "ENTITIES"
            && (record.Type == "3DSOLID" || record.Type == "REGION" || record.Type == "BODY");

        /// <summary>The bytes of the object of a handle in a DWG of R2000: its size, its data and its check sum.</summary>
        private static byte[] ObjectBytes(byte[] dwg, ulong handle)
        {
            DwgContainer container = DwgContainer.Read(dwg);
            long offset = container.ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(container.Objects, offset * 8, container.Objects.LongLength * 8);
            long size = reader.ModularShort();
            long head = reader.Position / 8 - offset;
            var bytes = new byte[head + size + 2];
            Array.Copy(container.Objects, offset, bytes, 0, bytes.Length);
            return bytes;
        }

        [Theory]
        [InlineData("solids_AC1015", 2)]
        [InlineData("splines_AC1015", 2)]
        public void TheBodiesOfAutoCadsDwgOfR2000_AreWrittenAgainToTheByte(string name, int bodies)
        {
            byte[] key = DwgAnswerKey.Dwg(name);
            string text = Encoding.UTF8.GetString(DwgReader.ToDxf(key));

            byte[] dwg = DwgWriter.ToDwg(text, "AC1015", out IDictionary<string, int> leftOut);

            List<DwgAnswerKey.Record> solids = DwgAnswerKey.Parse(DwgReader.ToDxf(key)).Where(IsBody).ToList();
            Assert.Equal(bodies, solids.Count);
            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);

            foreach (DwgAnswerKey.Record solid in solids)
            {
                ulong handle = ulong.Parse(Handle(solid), System.Globalization.NumberStyles.HexNumber);

                Assert.Equal(ObjectBytes(key, handle), ObjectBytes(dwg, handle));
            }
        }

        [Theory]
        [InlineData("acis_AC1018")]
        [InlineData("acis_AC1024")]
        [InlineData("acis_AC1032")]
        public void TheBoxSphereCylinderAndRegionOfADxfOfALaterVersion_AreWrittenToR2000AsAcis4Text(string name)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false)).ToText();

            byte[] dwg = DwgWriter.ToDwg(text, "AC1015", out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Where(IsBody).ToList();

            Assert.DoesNotContain("3DSOLID", leftOut.Keys);
            Assert.DoesNotContain("REGION", leftOut.Keys);
            Assert.Equal(3, back.Count(r => r.Type == "3DSOLID"));
            Assert.Equal(1, back.Count(r => r.Type == "REGION"));
            Assert.Null(DwgContainer.Read(dwg).DataStore);
            Assert.All(back, solid =>
                Assert.StartsWith(
                    "400 ",
                    DxfUnknownEntity.Plain(solid.Groups.First(g => g.Key == 1 && g.Value.Length > 0).Value)
                )
            );
        }

        [Fact]
        public void ASolidWhoseSatIsNotAcis4_StaysLeftOutOfR2000()
        {
            // The spline solids of R2010 have records ACIS 4.00 has no form for; R2000 holds that version alone.
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("splines_AC1024"), false)).ToText();

            DwgWriter.ToDwg(text, "AC1015", out IDictionary<string, int> leftOut);

            Assert.Equal(2, leftOut["3DSOLID"]);
        }

        [Fact]
        public void TheLinesOfTheSat_AreTheBytesOfTheFile_EachFollowedByACarriageReturnAndALineFeed()
        {
            // A caret DXF writes with a space after it is the caret alone in the file, and a line DXF starts with a space (where the
            // file has a tab, which AutoCAD writes in its DXF as a space) starts with a tab.
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("solids_AC1015"), false)).ToText();
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            DwgRecord solid = drawing.All.First(r => r.Type == "3DSOLID");
            int at = solid.Body.FindIndex(g => g.Key == 100 && g.Value == "AcDbModelerGeometry");
            int first = solid.Body.FindIndex(at, g => g.Key == 1);
            int second = solid.Body.FindIndex(first + 1, g => g.Key == 1);
            solid.Body[second] = new KeyValuePair<int, string>(1, " ab^ c");

            byte[] bytes = DwgObjectWriter.SatTextOf(solid);
            string lines = Encoding.ASCII.GetString(bytes);
            string[] split = lines.Split(new[] { "\r\n" }, StringSplitOptions.None);

            Assert.Equal("\tab^c", split[1]);
            Assert.Equal(string.Empty, split[split.Length - 1]);
            Assert.StartsWith("koo ", split[0], StringComparison.Ordinal);
        }
    }
}
