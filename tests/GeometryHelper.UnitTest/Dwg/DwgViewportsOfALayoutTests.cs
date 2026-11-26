using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The viewports of a layout made in code, written as DWG and read back: a viewport that was on is still on, its number
    /// is still the one it had, and the viewport that shows the sheet itself is still number 1.
    /// </summary>
    public class DwgViewportsOfALayoutTests
    {
        private static DxfDatabase LayoutWithAViewport()
        {
            var database = new DxfDatabase("AC1032");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0)));

            var layout = new DxfLayout { LayoutName = "S1" };
            layout.AddToLayoutDictionary(database, null);
            layout.CanonicalMediaName = "ISO_A3_(420.00_x_297.00_MM)";
            layout.Initialize();

            // As a drawing program makes one by MVIEW: the frame 15,55 to 255,285 on the sheet, the view about (50, 30) at 1:10.
            var viewport = new DxfViewport();
            layout.BlockTableRecord.AppendEntity(viewport);
            viewport.CenterPoint = new GeoPoint3(135, 170, 0);
            viewport.Width = 240;
            viewport.Height = 230;
            viewport.ViewCenter = new GeoPoint2(50, 30);
            viewport.ViewHeight = 2300;
            viewport.On = true;
            return database;
        }

        private static DxfDatabase ThroughDwg(DxfDatabase database, string version)
        {
            var stream = new MemoryStream();
            DwgWriter.Write(database, stream, version);
            stream.Position = 0;
            return DwgReader.Open(stream);
        }

        private static DxfLayout S1(DxfDatabase database) => database.Layouts.Single(l => l.LayoutName == "S1");

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1024")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        [InlineData("AC1015")]
        public void TheViewportsOfALayoutWrittenAsDwgAndReadBack_AreOnAndNumberedAsBefore(string version)
        {
            DxfDatabase before = LayoutWithAViewport();
            DxfViewport[] was = S1(before).GetViewports().ToArray();
            DxfViewport[] now = S1(ThroughDwg(before, version)).GetViewports().ToArray();

            Assert.Equal(new[] { 1, 2 }, was.Select(v => v.Number));
            Assert.Equal(was.Select(v => v.On), now.Select(v => v.On));
            Assert.Equal(was.Select(v => v.Number), now.Select(v => v.Number));
            Assert.Equal(new[] { true, true }, now.Select(v => v.On));
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1015")]
        public void AViewportTurnedOff_StaysOff_AndTheOnesBesideItStayOn_AfterDwg(string version)
        {
            DxfDatabase before = LayoutWithAViewport();
            DxfLayout layout = S1(before);
            var off = new DxfViewport();
            layout.BlockTableRecord.AppendEntity(off);
            off.CenterPoint = new GeoPoint3(300, 170, 0);
            off.Width = 50;
            off.Height = 50;

            DxfViewport[] now = S1(ThroughDwg(before, version)).GetViewports().ToArray();

            Assert.Equal(new[] { true, true, false }, now.Select(v => v.On));
            Assert.Equal(new[] { 1, 2, -1 }, now.Select(v => v.Number));

            // The viewport made as MVIEW makes it keeps its frame and its view: 240 by 230 on the sheet showing 2400 by 2300.
            Assert.Equal(240.0, now[1].Width, 1E-9);
            Assert.Equal(230.0, now[1].Height, 1E-9);
            Assert.Equal(2300.0, now[1].ViewHeight, 1E-9);
            Assert.Equal(50.0, now[1].ViewCenter.X, 1E-9);
            Assert.Equal(30.0, now[1].ViewCenter.Y, 1E-9);
        }
    }
}
