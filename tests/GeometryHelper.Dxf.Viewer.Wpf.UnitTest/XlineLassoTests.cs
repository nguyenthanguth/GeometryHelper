using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A lasso over an XLINE, as AutoCAD's: a crossing lasso it runs through takes it, a window lasso never does.</summary>
    public class XlineLassoTests
    {
        private static CadDocument Document()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfXline { BasePoint = new GeoPoint3(200, 300, 0), UnitDir = new GeoVector3(1, 0, 0) });
            return CadDocument.From(database, "xline.dxf");
        }

        [Fact]
        public void ACrossingLassoTheXlineRunsThrough_TakesIt_AndAWindowLassoOrOneBesideItDoesNot()
        {
            CadDocument document = Document();
            string handle = Assert.Single(document.ModelSpace.Block).Handle;

            // Counter-clockwise round a box 10 by 20 far along the xline: a crossing.
            var crossing = new[] { new GeoPoint2(500, 290), new GeoPoint2(510, 290), new GeoPoint2(510, 310), new GeoPoint2(500, 310) };
            Assert.Equal(new[] { handle }, CadPicking.LassoDone(document, document.ModelSpace, null, crossing, false));

            // The same box clockwise: a window, which a line without end never lies inside.
            document.Selection.Clear();
            var window = new[] { new GeoPoint2(500, 290), new GeoPoint2(500, 310), new GeoPoint2(510, 310), new GeoPoint2(510, 290) };
            Assert.Empty(CadPicking.LassoDone(document, document.ModelSpace, null, window, false));

            // A crossing above the line touches nothing.
            var beside = new[] { new GeoPoint2(500, 310), new GeoPoint2(510, 310), new GeoPoint2(510, 320), new GeoPoint2(500, 320) };
            Assert.Empty(CadPicking.LassoDone(document, document.ModelSpace, null, beside, false));
        }
    }
}
