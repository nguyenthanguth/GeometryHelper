using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The layer Defpoints added for an entity put on it, as the points of definition of a dimension are: written not plotted,
    /// as AutoCAD 2021 refuses to read a drawing whose Defpoints is plotted ("Invalid AcDbLayerTableRecord plot flag for
    /// DEFPOINTS layer").
    /// </summary>
    public class DxfDefpointsLayerTests
    {
        [Fact]
        public void DefpointsAddedForAPoint_IsWrittenNotPlotted_AndAnotherLayerIsPlotted()
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(new DxfDBPoint(new GeoPoint3(0, 0, 0)) { Layer = "Defpoints" });
            db.ModelSpace.AppendEntity(new DxfDBPoint(new GeoPoint3(1, 0, 0)) { Layer = "Steel" });

            DxfDatabase read = DxfDatabase.Parse(db.ToText());

            Assert.False(read.LayerTable["Defpoints"].IsPlottable);
            Assert.True(read.LayerTable["Steel"].IsPlottable);
            string defpoints = db.ToText()
                .Split(new[] { "  0\nLAYER\n" }, StringSplitOptions.None)
                .Skip(1)
                .Select(r =>
                    r.IndexOf("\n  0\n", StringComparison.Ordinal) < 0
                        ? r
                        : r.Substring(0, r.IndexOf("\n  0\n", StringComparison.Ordinal))
                )
                .Single(r => r.Contains("\n  2\nDefpoints\n"));
            Assert.Contains(" 70\n0\n 62\n7\n  6\nContinuous\n290\n0\n", defpoints, StringComparison.OrdinalIgnoreCase);
        }
    }
}
