using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A DWG of AutoCAD 2000 to 2007 keeps the cell styles of a table style for a round trip in two CELLSTYLEMAP objects, which
    /// were left out of the DXF made of it: AutoCAD's DXF of the same drawing has them, each of three cell styles, each style
    /// its format, six grid formats, id, kind and name.
    /// </summary>
    public class DwgCellStyleMapTests
    {
        private static List<DwgAnswerKey.Record> Maps(byte[] dxf) =>
            DwgAnswerKey
                .Parse(dxf)
                .Where(r => r.Type == "CELLSTYLEMAP")
                .OrderBy(r => r.Owner, System.StringComparer.Ordinal)
                .ToList();

        [Theory]
        [InlineData("table_AC1015")]
        [InlineData("table_AC1018")]
        [InlineData("table_AC1021")]
        public void TheCellStyleMapsOfATableStyle_AreReadAsAutoCADsDxfGivesThem(string key)
        {
            List<DwgAnswerKey.Record> expected = Maps(DwgAnswerKey.Key(key));
            List<DwgAnswerKey.Record> got = Maps(DwgReader.ToDxf(DwgAnswerKey.Dwg(key)));

            Assert.Equal(2, expected.Count);
            Assert.Equal(expected.Count, got.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                // AutoCAD gives each map a new handle in DXF, and makes them in another order; the maps stand by their owners.
                Assert.Equal(
                    expected[i].Groups.Where(g => g.Key != 5).Select(g => g.Key + "=" + g.Value.Trim()),
                    got[i].Groups.Where(g => g.Key != 5).Select(g => g.Key + "=" + g.Value.Trim())
                );
            }
        }
    }
}
