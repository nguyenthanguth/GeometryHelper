using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The VIEW table: a named view added as AutoCAD 2021's -VIEW Window writes one, read back, changed, given a layer
    /// snapshot, renamed and erased with its extension dictionary.
    /// </summary>
    public class DxfViewTableRecordTests
    {
        private const string Head = "100\nAcDbSymbolTableRecord\n100\nAcDbViewTableRecord\n";

        [Fact]
        public void AViewAdded_IsWrittenAsAutoCad2021WroteTheWindow10By20To110By80_AndReadBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.Empty(db.ViewTable);

            db.ViewTable.Add(new DxfViewTableRecord { Name = "V2", CenterPoint = new GeoPoint2(60.0, 50.0), Height = 60.0, Width = 100.0 });
            string saved = db.ToText();
            Assert.Contains(Head + "  2\nV2\n 70\n0\n 40\n60\n 10\n60\n 20\n50\n 41\n100\n 11\n0.0\n 21\n0.0\n 31\n1.0\n 12\n0.0\n 22\n0.0\n 32\n0.0\n 42\n50.0\n 43\n0.0\n 44\n0.0\n 50\n0.0\n 71\n0\n281\n0\n 72\n0\n 73\n0\n", saved, StringComparison.Ordinal);

            DxfViewTableRecord view = DxfDatabase.Parse(saved).ViewTable["v2"];
            Assert.Equal("V2", view.Name);
            Assert.Equal(new GeoPoint2(60.0, 50.0), view.CenterPoint);
            Assert.Equal(60.0, view.Height);
            Assert.Equal(100.0, view.Width);
            Assert.False(view.IsPaperspaceView);
            Assert.Equal(GeoVector3.ZAxis, view.ViewDirection);
            Assert.Equal(string.Empty, view.LayerState);
        }

        [Fact]
        public void AViewOfAFile_IsChangedGivenASnapshotRenamedAndErased_ItsExtensionDictionaryWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.ViewTable.Add(new DxfViewTableRecord { Name = "Stair core", CenterPoint = new GeoPoint2(18450.0, 6200.0), Height = 9000.0, Width = 14400.0 });
            DxfDatabase file = DxfDatabase.Parse(db.ToText());
            string before = file.ToText();

            // Setting the values a view has changes no byte.
            DxfViewTableRecord view = file.ViewTable["Stair core"];
            view.CenterPoint = new GeoPoint2(18450.0, 6200.0);
            view.Height = 9000.0;
            Assert.Equal(before, file.ToText());

            view.Width = 15000.0;
            view.IsPaperspaceView = true;
            view.LayerState = "ACAD_VIEWS_Stair core";
            view.Name = "Core";
            Assert.Throws<ArgumentException>(() => view.Height = 0.0);

            DxfDatabase again = DxfDatabase.Parse(file.ToText());
            DxfViewTableRecord core = again.ViewTable["Core"];
            Assert.Equal(15000.0, core.Width);
            Assert.True(core.IsPaperspaceView);
            Assert.Equal("ACAD_VIEWS_Stair core", core.LayerState);
            Assert.Equal(new[] { "290=0", "302=ACAD_VIEWS_Stair core", "293=0", "294=0" }, ((DxfXrecord)core.ExtensionDictionary.GetAt("ADSK_XREC_VTRVIEWINFO")).Data.Select(g => g.TypeCode + "=" + g.Value));

            string dictionary = core.ExtensionDictionary.Handle;
            core.Erase();
            Assert.Empty(again.ViewTable);
            Assert.False(again.TryGetObject(dictionary, out _));
            Assert.Empty(DxfDatabase.Parse(again.ToText()).ViewTable);
        }
    }
}
