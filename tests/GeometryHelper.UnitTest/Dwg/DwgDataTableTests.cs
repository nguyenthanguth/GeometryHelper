using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The DATATABLE a table of AutoCAD 2000 and 2004 keeps under its round-trip record, in place of the TABLECONTENT of
    /// later versions (ACDBDATATABLE in the DXF of R2000): its columns of numbers and handles, left out until its layout
    /// was read from the DWG AutoCAD wrote beside its own DXF of the same table.
    /// </summary>
    public class DwgDataTableTests
    {
        private static List<string> DataTables(byte[] dxf) =>
            DwgAnswerKey
                .Parse(dxf)
                .Where(r => r.Type == "DATATABLE" || r.Type == "ACDBDATATABLE")
                .Select(r =>
                    r.Type
                    + " "
                    + string.Join(
                        " ",
                        r.Groups.Where(g => g.Key != 5 && g.Key != 330 && g.Key != 360)
                            .Select(g => g.Key + "=" + g.Value.Trim())
                    )
                    + " handles "
                    + r.Groups.Count(g => g.Key == 360)
                )
                .ToList();

        [Theory]
        [InlineData("table_AC1015")]
        [InlineData("table_AC1018")]
        public void TheDataTablesOfATable_ReadFromAutoCadsDwg_AreThoseOfItsDxf_ButForTheirHandles(string name)
        {
            List<string> key = DataTables(DwgAnswerKey.Key(name));
            List<string> got = DataTables(DwgReader.ToDxf(DwgAnswerKey.Dwg(name)));

            Assert.Equal(2, key.Count);
            Assert.Equal(key, got);
        }

        [Theory]
        [InlineData("table_AC1015")]
        [InlineData("table_AC1018")]
        public void TheDataTablesOfATable_AreLeftOutOfTheDwgOfR2018_WhichKeepsATableContent(string name)
        {
            byte[] dwg = DwgWriter.ToDwg(
                System.Text.Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(name))),
                "AC1032",
                out IDictionary<string, int> leftOut
            );

            Assert.DoesNotContain("DATATABLE", leftOut.Keys);
            Assert.DoesNotContain("ACDBDATATABLE", leftOut.Keys);
            Assert.Empty(DataTables(DwgReader.ToDxf(dwg)));
        }
    }
}
