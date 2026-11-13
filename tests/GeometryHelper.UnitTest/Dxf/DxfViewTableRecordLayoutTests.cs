using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The layout a view of paper space was saved in, group 330 of the XRECORD ADSK_XREC_VTRVIEWINFO of its extension
    /// dictionary, as AutoCAD 2021 wrote it for -VIEW Save in Layout1 (290 and 292 of nought, the 330, a 294 of nought).
    /// </summary>
    public class DxfViewTableRecordLayoutTests
    {
        private static DxfViewTableRecord Added(DxfDatabase db, string name, bool paper)
        {
            db.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = name,
                    IsPaperspaceView = paper,
                    CenterPoint = new GeoPoint2(60.0, 50.0),
                    Height = 60.0,
                    Width = 100.0,
                }
            );
            return db.ViewTable[name];
        }

        private static string[] Info(DxfViewTableRecord view) =>
            ((DxfXrecord)view.ExtensionDictionary.GetAt("ADSK_XREC_VTRVIEWINFO"))
                .Data.Select(g => g.TypeCode + "=" + g.Value)
                .ToArray();

        [Fact]
        public void AViewGivenALayout_WritesItAsAutoCadWroteIt_AndReadsItBackThroughAFile()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLayout layout = db.Layouts.First(l => l.LayoutName != "Model");
            DxfViewTableRecord view = Added(db, "PV", true);
            Assert.Null(view.Layout);

            view.Layout = layout;

            Assert.Equal(new[] { "290=0", "292=0", "330=" + layout.Handle, "294=0" }, Info(view));
            DxfViewTableRecord again = DxfDatabase.Parse(db.ToText()).ViewTable["PV"];
            Assert.Equal(layout.Handle, again.Layout.Handle);
            Assert.Equal(string.Empty, again.LayerState);
        }

        [Fact]
        public void ALayoutSet_KeepsTheLayerSnapshot_AndALayerSnapshotSet_KeepsTheLayout()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLayout layout = db.Layouts.First(l => l.LayoutName != "Model");
            DxfViewTableRecord view = Added(db, "PV", true);
            view.LayerState = "ACAD_VIEWS_PV";

            view.Layout = layout;
            Assert.Equal("ACAD_VIEWS_PV", view.LayerState);
            Assert.Equal(new[] { "290=0", "302=ACAD_VIEWS_PV", "293=0", "330=" + layout.Handle, "294=0" }, Info(view));

            view.LayerState = "ACAD_VIEWS_Other";
            Assert.Equal(layout.Handle, view.Layout.Handle);
            Assert.Equal(
                new[] { "290=0", "302=ACAD_VIEWS_Other", "293=0", "330=" + layout.Handle, "294=0" },
                Info(view)
            );
        }

        [Fact]
        public void ANullLayout_TakesTheHandleOut_AndTheSameLayoutSetAgainChangesNoByte()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLayout layout = db.Layouts.First(l => l.LayoutName != "Model");
            DxfViewTableRecord view = Added(db, "PV", true);
            view.Layout = layout;
            string before = db.ToText();

            view.Layout = layout;
            Assert.Equal(before, db.ToText());

            view.Layout = null;
            Assert.Null(view.Layout);
            Assert.Equal(new[] { "290=0", "292=0", "294=0" }, Info(view));
            Assert.Null(Added(db, "MV", false).Layout);
        }

        [Fact]
        public void AViewInNoFile_CannotBeGivenALayout()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLayout own = db.Layouts.First(l => l.LayoutName != "Model");

            Assert.Throws<InvalidOperationException>(() => new DxfViewTableRecord().Layout = own);
        }
    }
}
