using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The text height and text style a table keeps of its own for the rows of a kind, as AutoCAD 2021's
    /// <c>TextHeight(RowType)</c> and <c>TextStyle(RowType)</c> give them (D41d): what the presenter lays a row out by.
    /// </summary>
    public class DxfTableOwnTextLooksTests
    {
        /// <summary>The R2018 drawing of D33b's table of its own data looks: its data of 9 high in GH_TXT, red and top left.</summary>
        private static DxfTable OwnData()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(
                DxfDimensionDrawings.R2018.Replace(
                    Head,
                    " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n"
                )
            );
            return db.ModelSpace.OfType<DxfTable>().First();
        }

        [Fact]
        public void ATableOfItsOwnHeaderAndDataLooks_GivesTheirTextHeightAndStyle_ForThoseRows()
        {
            DxfTable table = OwnData();
            table.OwnTextLooks(out double?[] heights, out string[] styles);

            // Title, header, data: the header and the data are 9 high of the table's own, the data in GH_TXT, the title nothing.
            Assert.Null(heights[0]);
            Assert.Equal(9.0, heights[1]);
            Assert.Equal(9.0, heights[2]);
            Assert.Null(styles[0]);
            Assert.Null(styles[1]);
            Assert.Equal("GH_TXT", styles[2]);
        }

        [Fact]
        public void ATableOfNoLooksOfItsOwn_GivesNone()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            db.ModelSpace.OfType<DxfTable>().First().OwnTextLooks(out double?[] heights, out string[] styles);

            Assert.All(heights, h => Assert.Null(h));
            Assert.All(styles, s => Assert.Null(s));
        }
    }
}
