using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A click on the canvas with Shift held reaches TRIM as a Shift-pick, which extends the object instead.</summary>
    public class TrimShiftClickTests
    {
        [Fact]
        public void AShiftClickAtTrimsPrompt_ExtendsTheLineToTheWall_AndAPlainClickTrimsIt()
        {
            var database = new DxfDatabase("AC1027");
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0));
            database.ModelSpace.AppendEntity(line);
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, -10, 0), new GeoPoint3(100, 10, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(20, -10, 0), new GeoPoint3(20, 10, 0)));
            CadDocument document = CadDocument.From(database, "shift.dxf");
            var session = new CadCommandSession(document) { Tolerance = Tolerance.Default };
            session.Feed("TR");

            Assert.True(CadPicking.Click(document, document.ModelSpace, session, null, new GeoPoint2(35, 0), 1.0, true));
            var extended = (DxfLine)document.FindEntity(line.Handle);
            Assert.Equal(100.0, extended.EndPoint.X, 9);

            Assert.True(CadPicking.Click(document, document.ModelSpace, session, null, new GeoPoint2(10, 0), 1.0, false));
            var trimmed = (DxfLine)document.FindEntity(line.Handle);
            Assert.Equal(20.0, trimmed.StartPoint.X, 9);
            Assert.Equal(100.0, trimmed.EndPoint.X, 9);
            session.Enter();
            Assert.Equal("TRIM", document.NextUndo.Name);
        }
    }
}
