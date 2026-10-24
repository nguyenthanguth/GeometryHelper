using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The VERTEX of a 3D POLYLINE as a <see cref="DxfPolylineVertex3d"/>: made in code, flagged 32, and read and moved through
    /// its polyline.
    /// </summary>
    public class DxfPolylineVertex3dTests
    {
        [Fact]
        public void AVertexMadeInCode_IsAPointOfTheWorld_FlaggedAsOneOfA3DPolyline()
        {
            var vertex = new DxfPolylineVertex3d(new GeoPoint3(1, 2, 3));

            Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(vertex.Position));
            Assert.Equal(DxfVertex3dType.SimpleVertex, vertex.VertexType);
            vertex.TransformBy(DxfPolylineTests.Turn());
            Assert.Equal(Bits(DxfPolylineTests.Turn().Transform(new GeoPoint3(1, 2, 3))), Bits(vertex.Position));
            Assert.Throws<ArgumentException>(() => new DxfPolylineVertex3d(new GeoPoint3(double.NaN, 0, 0)));
        }

        [Fact]
        public void AVertexOfAPolyline_IsMovedInItsOwnRecord()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfPolylineVertex3d vertex = ByHandle<DxfPolyline3d>(db, "47").ElementAt(1);
            string handle = vertex.Handle;

            vertex.Position = new GeoPoint3(-1, -2, -3);

            DxfPolylineVertex3d again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(db.ToText()), "47").ElementAt(1);
            Assert.Equal(handle, again.Handle);
            Assert.Equal(Bits(new GeoPoint3(-1, -2, -3)), Bits(again.Position));
        }
    }
}
