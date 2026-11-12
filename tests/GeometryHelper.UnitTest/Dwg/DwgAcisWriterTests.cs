using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// REGION, 3DSOLID and BODY written into a DWG of R2018: each entity with the GUID of its ACIS data, and the data in the
    /// data store of the file (AcDb:AcDsPrototype_1b) as AutoCAD lays it out, read back to the same records.
    /// </summary>
    /// <remarks>
    /// acis_AC1032 was made by probe GhW4b in AutoCAD 2021: a box 30 by 20 by 10, a sphere of radius 8, a cylinder of radius
    /// 5 and a region of a circle; acisbig_AC1032 a plate 400 by 400 by 10 with 20 by 20 holes, 515 873 bytes of SAB.
    /// </remarks>
    public class DwgAcisWriterTests
    {
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

        /// <summary>
        /// The store with the bytes AutoCAD leaves unset blanked: those after the entries of the index of segments, up to
        /// the end of its segment, which AutoCAD fills with what its memory held and the writer with 0x70.
        /// </summary>
        private static byte[] Blanked(byte[] store)
        {
            byte[] copy = (byte[])store.Clone();
            int count = BitConverter.ToInt32(copy, 32);
            int from = 0x80 + 0x30 + (count * 12);
            int to = 0x80 + BitConverter.ToInt32(copy, 0x80 + 16);

            for (int i = from; i < to; i++)
            {
                copy[i] = 0;
            }

            return copy;
        }

        [Fact]
        public void SolidsAndARegion_WrittenAsDwg_ReadBackWithTheirAcisData()
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgAnswerKey.Key("acis_AC1032"),
                DwgReader.ToDxf(Written(Source("acis_AC1032")))
            );

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing);
            Assert.True(result.Paired > 390, result.Paired + " records paired");
        }

        [Fact]
        public void TheDataStoreOfTheSolids_IsAutoCadsOwnBytes_ButThoseAutoCadLeavesUnset()
        {
            // The four records in the order of AutoCAD's own DXF (the region first), the schemas, the search segment.
            byte[] ours = DwgContainer.Read(Written(Source("acis_AC1032"))).DataStore;
            byte[] autocad = DwgContainer.Read(DwgAnswerKey.Dwg("acis_AC1032")).DataStore;

            Assert.Equal(11520, ours.Length);
            Assert.Equal(Blanked(autocad), Blanked(ours));
        }

        [Fact]
        public void APlateOf400Holes_IsWrittenWithItsDataInAPageOfItsOwn_AndReadBack()
        {
            byte[] dxf = DwgReader.ToDxf(DwgAnswerKey.Dwg("acisbig_AC1032"));
            byte[] dwg = Written(DxfDatabase.Open(new MemoryStream(dxf, false)));
            byte[] ours = DwgContainer.Read(dwg).DataStore;
            byte[] autocad = DwgContainer.Read(DwgAnswerKey.Dwg("acisbig_AC1032")).DataStore;

            Assert.Equal(Blanked(autocad), Blanked(ours));
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(dxf, DwgReader.ToDxf(dwg));
            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing);
        }

        [Fact]
        public void ADrawingOfSolids_IsWrittenInTheSameBytes()
        {
            Assert.Equal(Written(Source("acis_AC1032")), Written(Source("acis_AC1032")));
        }

        [Fact]
        public void ADrawingWithNoSolids_HasNoDataStore()
        {
            Assert.Null(DwgContainer.Read(Written(Source("curves_AC1032"))).DataStore);
        }

        [Fact]
        public void ASolidOfSatText_AsADxfOfR2000GivesIt_IsLeftOut()
        {
            // The DXF of R2000 gives the box and the region as SAT text, which a DWG of R2018 keeps as SAB only.
            DwgWriter.ToDwg(Source("solids_AC1015").ToText(), "AC1032", out IDictionary<string, int> leftOut);

            Assert.Equal(1, leftOut["3DSOLID"]);
            Assert.Equal(1, leftOut["REGION"]);
        }
    }
}
