using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The property of a dimension that sets a variable of its dimension style writes the override whether or not the value is
    /// the style's own, as AutoCAD 2021's does (checked in its console, 13 November: DIMASZ, DIMTXT, DIMSCALE, DIMTAD, DIMSE1,
    /// DIMCLRD, DIMLWD, DIMJOGANG, DIMTXSTY, DIMLTYPE and DIMBLK each written at the style's value, 0.18 as 1070 41, 1040 0.18).
    /// </summary>
    public class DxfDimensionSetterOverrideTests
    {
        private static DxfRotatedDimension Dimension(out DxfDatabase db)
        {
            db = new DxfDatabase("AC1032");
            var dim = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 20, 0),
                string.Empty,
                null
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            dim.DimBlock = picture;
            db.ModelSpace.AppendEntity(dim);
            return dim;
        }

        /// <summary>The list DSTYLE of the dimension as saved, from "{" to "}", one group to a line.</summary>
        private static string Overrides(DxfDatabase db, DxfDimension dim)
        {
            string record = Records(db.ToText()).Single(r => HandleOf(r) == dim.Handle);
            int open = record.IndexOf("1002\n{\n", StringComparison.Ordinal);
            int close = record.LastIndexOf("1002\n}\n", StringComparison.Ordinal);
            return open < 0 ? string.Empty : record.Substring(open + 7, close - open - 7);
        }

        [Fact]
        public void ASizeSetToTheStylesValue_IsWrittenAsAnOverride()
        {
            DxfRotatedDimension dim = Dimension(out DxfDatabase db);
            Assert.Equal(string.Empty, Overrides(db, dim));

            dim.Dimasz = dim.Dimasz;
            dim.Dimtxt = dim.Dimtxt;
            dim.Dimscale = dim.Dimscale;

            Assert.Equal("1070\n41\n1040\n0.18\n1070\n140\n1040\n0.18\n1070\n40\n1040\n1\n", Overrides(db, dim));
        }

        [Fact]
        public void AFlagAWholeNumberAndAColourSetToTheStylesValue_AreWrittenAsOverrides()
        {
            DxfRotatedDimension dim = Dimension(out DxfDatabase db);

            dim.Dimtad = dim.Dimtad;
            dim.Dimse1 = dim.Dimse1;
            dim.Dimclrd = dim.Dimclrd;

            Assert.Equal("1070\n77\n1070\n0\n1070\n75\n1070\n0\n1070\n176\n1070\n0\n", Overrides(db, dim));
        }

        [Fact]
        public void ARecordSetToTheStylesRecord_IsWrittenAsAnOverrideOfIt()
        {
            DxfRotatedDimension dim = Dimension(out DxfDatabase db);
            DxfDimStyleTableRecord style = db.DimStyleTable["Standard"];

            dim.TextStyle = style.Dimtxsty;

            Assert.Equal("1070\n340\n1005\n" + style.Dimtxsty.Handle + "\n", Overrides(db, dim));
        }

        [Fact]
        public void AValueSetTwice_IsWrittenOnce()
        {
            DxfRotatedDimension dim = Dimension(out DxfDatabase db);

            dim.Dimasz = dim.Dimasz;
            dim.Dimasz = dim.Dimasz;
            Assert.Equal("1070\n41\n1040\n0.18\n", Overrides(db, dim));

            dim.Dimasz = 2.5;
            dim.Dimasz = 2.5;
            Assert.Equal("1070\n41\n1040\n2.5\n", Overrides(db, dim));

            dim.Dimasz = 0.18;
            Assert.Equal("1070\n41\n1040\n0.18\n", Overrides(db, dim));
        }
    }
}
