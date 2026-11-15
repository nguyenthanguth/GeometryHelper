using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Vertices added one by one to a LWPOLYLINE already in a drawing, each written by splicing its groups into the record's
    /// bytes. The record was grown from nothing and copied again for each vertex, so that 10 000 vertices took 1.3 s under
    /// .NET Framework 4.8; written into one array of its new length, they take 0.45 s. The limit is some five times that, and
    /// is there to catch a writing that costs more each vertex again.
    /// </summary>
    public class DxfPolylineSpeedTests
    {
        [Fact]
        public void TenThousandVerticesAddedOneByOneToAPolylineInADrawing_TakeUnderTwoAndAHalfSeconds()
        {
            DxfPolyline polyline = null;
            SpeedGuard.AssertWithin(
                2.5,
                () =>
                {
                    var database = new DxfDatabase("AC1027");
                    polyline = new DxfPolyline();
                    database.ModelSpace.AppendEntity(polyline);

                    return polyline;
                },
                added =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        added.AddVertexAt(i, new GeoPoint2(i, i % 7), 0, 0, 0);
                    }
                }
            );

            Assert.Equal(10000, polyline.NumberOfVertices);
            Assert.True(polyline.GetPoint2dAt(9999).IsEqualTo(new GeoPoint2(9999, 3), Tolerance.Default));
        }
    }
}
