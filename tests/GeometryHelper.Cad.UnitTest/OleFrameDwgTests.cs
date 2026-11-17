using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A DWG that holds a picture pasted, opened as the presenter opens one: the picture is drawn. The DWG reader once left
    /// every OLE2FRAME out, so a drawing AutoCAD shows with its pictures opened with none.
    /// </summary>
    public class OleFrameDwgTests
    {
        [Fact]
        public void ABitmapFrameOfADwg_IsDrawnAsItsPicture_WhereItsFrameIs()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var frame = new DxfOle2Frame(OleFrameDisplayTests.RedAndBlueBitmap());
            database.ModelSpace.AppendEntity(frame);
            frame.Position3d = new DxfRectangle3d(
                new GeoPoint3(10, 40, 0),
                new GeoPoint3(50, 40, 0),
                new GeoPoint3(10, 20, 0),
                new GeoPoint3(50, 20, 0)
            );
            string path = Path.Combine(Path.GetTempPath(), "gh-oledwg-" + System.Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(database, path);
                CadDocument document = CadDocument.Open(path);

                DisplayImage2 picture = Assert.Single(
                    document.GetDisplay(document.ModelSpace).Items.OfType<DisplayImage2>()
                );
                Assert.Equal(10.0, picture.Bounds.MinX, 9);
                Assert.Equal(20.0, picture.Bounds.MinY, 9);
                Assert.Equal(50.0, picture.Bounds.MaxX, 9);
                Assert.Equal(40.0, picture.Bounds.MaxY, 9);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
