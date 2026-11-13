using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>A linetype of a drawing renamed with every reference to it by name, as AutoCAD's -RENAME LType does.</summary>
    public class DxfLinetypeRenameTests
    {
        private static DxfDatabase DrawingWithDashed()
        {
            var db = new DxfDatabase("AC1027");
            var dashed = new DxfLinetypeTableRecord { Name = "GH_DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 0.5);
            dashed.SetDashLengthAt(1, -0.25);
            db.LinetypeTable.Add(dashed);
            return db;
        }

        [Fact]
        public void ALinetypeRenamed_TakesItsEntitiesItsLayersAndTheCurrentLinetypeWithIt()
        {
            DxfDatabase db = DrawingWithDashed();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "GH_STEEL", LinetypeName = "GH_DASHED" });
            db.Celtype = "GH_DASHED";
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Linetype = "GH_DASHED" };
            db.ModelSpace.AppendEntity(line);

            db.LinetypeTable["GH_DASHED"].Name = "GH_HIDDEN";

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.LinetypeTable.Has("GH_DASHED"));
            Assert.True(read.LinetypeTable.Has("GH_HIDDEN"));
            Assert.Equal("GH_HIDDEN", read.LayerTable["GH_STEEL"].LinetypeName);
            Assert.Equal("GH_HIDDEN", read.Celtype);
            Assert.Equal("GH_HIDDEN", read.ModelSpace.OfType<DxfLine>().Single().Linetype);
            Assert.Equal(0.5, read.LinetypeTable["GH_HIDDEN"].DashLengthAt(0));
        }

        [Fact]
        public void AViewportRenamed_IsItsRecordNameChanged_AndTheOtherRecordsAreLeftAlone()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            int count = db.ViewportTable.Count();
            DxfViewportTableRecord first = db.ViewportTable.First();

            first.Name = "GH_TILES";

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(count, read.ViewportTable.Count());
            Assert.Contains(read.ViewportTable, v => v.Name == "GH_TILES");
            Assert.Throws<ArgumentException>(() => first.Name = "a:b");
        }

        [Fact]
        public void ALinetypeRenamedToTheNameOfAnother_OrOneAutoCadKeepsRenamed_IsRefused()
        {
            DxfDatabase db = DrawingWithDashed();
            db.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "GH_CENTER" });

            Assert.Throws<ArgumentException>(() => db.LinetypeTable["GH_DASHED"].Name = "gh_center");
            Assert.Throws<InvalidOperationException>(() => db.LinetypeTable["Continuous"].Name = "GH_SOLID");
            Assert.Throws<InvalidOperationException>(() => db.LinetypeTable["ByLayer"].Name = "GH_LAYER");
            Assert.Throws<InvalidOperationException>(() => db.LinetypeTable["ByBlock"].Name = "GH_BLOCK");
            Assert.Throws<ArgumentException>(() => db.LinetypeTable["GH_DASHED"].Name = "a:b");
        }
    }
}
