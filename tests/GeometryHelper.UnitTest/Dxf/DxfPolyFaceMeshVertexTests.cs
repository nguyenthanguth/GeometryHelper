using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>The point of a polyface mesh as a <see cref="DxfPolyFaceMeshVertex"/>: read and moved in its own record.</summary>
    public class DxfPolyFaceMeshVertexTests
    {
        [Fact]
        public void APointOfAMesh_IsMovedInItsOwnRecord_TheMeshsBodyWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyFaceMesh mesh = ByHandle<DxfPolyFaceMesh>(db, "4E");
            DxfPolyFaceMeshVertex point = mesh.OfType<DxfPolyFaceMeshVertex>().ElementAt(6);

            Assert.Equal("57", point.Handle);
            point.Position = new GeoPoint3(1, 1, 2);

            DxfPolyFaceMesh again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(db.ToText()), "4E");
            Assert.Equal(Bits(new GeoPoint3(1, 1, 2)), Bits(again.OfType<DxfPolyFaceMeshVertex>().ElementAt(6).Position));
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(new DxfPolyFaceMeshVertex().Position));
        }
    }
}
