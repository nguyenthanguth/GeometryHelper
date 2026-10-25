using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>The point of a polygon mesh as a <see cref="DxfPolygonMeshVertex"/>: appended to a mesh and moved in its own record.</summary>
    public class DxfPolygonMeshVertexTests
    {
        [Fact]
        public void APointAppendedToAMesh_IsMovedInItsOwnRecord()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mesh = new DxfPolygonMesh();
            mesh.MSize = 2;
            mesh.NSize = 2;

            foreach (GeoPoint3 p in new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0), new GeoPoint3(1, 1, 0) })
            {
                mesh.AppendVertex(new DxfPolygonMeshVertex(p));
            }

            db.ModelSpace.AppendEntity(mesh);
            db = DxfDatabase.Parse(db.ToText());
            DxfPolygonMeshVertex point = ByHandle<DxfPolygonMesh>(db, mesh.Handle).Last();
            point.Position = new GeoPoint3(1, 1, 0.5);
            Assert.Equal(mesh.Handle, point.OwnerHandle);

            DxfPolygonMesh again = ByHandle<DxfPolygonMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
            Assert.Equal(4, again.Count());
            Assert.Equal(Bits(new GeoPoint3(1, 1, 0.5)), Bits(again.Last().Position));
            Assert.Equal(point.Handle, again.Last().Handle);
        }
    }
}
