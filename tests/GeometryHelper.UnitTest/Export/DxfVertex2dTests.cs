using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The VERTEX of a POLYLINE of the plane as a <see cref="DxfVertex2d"/>: made in code and set before it is appended, read
    /// through its polyline, and erased from it.
    /// </summary>
    public class DxfVertex2dTests
    {
        [Fact]
        public void AVertexMadeInCode_HoldsWhatItIsGiven_UntilAppended()
        {
            var vertex = new DxfVertex2d(new GeoPoint3(5, 1, 2), 0.5, 0.25, 0.75, Math.PI / 2);

            Assert.Null(vertex.Handle);
            Assert.Equal(Bits(new GeoPoint3(5, 1, 2)), Bits(vertex.Position));
            Assert.Equal(0.5, vertex.Bulge);
            Assert.Equal(0.25, vertex.StartWidth);
            Assert.Equal(0.75, vertex.EndWidth);
            Assert.Equal(Math.PI / 2, vertex.Tangent, 15);
            Assert.False(vertex.TangentUsed);
            Assert.Equal(DxfVertex2dType.SimpleVertex, vertex.VertexType);

            vertex.Bulge = 0.0;
            vertex.Position = new GeoPoint3(6, 1, 2);
            Assert.Equal(0.0, vertex.Bulge);
            Assert.Throws<InvalidOperationException>(() => vertex.Erase());
            Assert.Throws<ArgumentOutOfRangeException>(() => vertex.StartWidth = -1.0);
        }

        [Fact]
        public void AVertexOfAPolyline_IsReadThroughIt_AndErasedFromIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");
            DxfVertex2d vertex = polyline.ElementAt(1);
            var appended = new DxfVertex2d(new GeoPoint3(0, 5, 9), 0.0, 0.0, 0.0, 0.0);

            polyline.Elevation = 3.0;
            polyline.AppendVertex(appended);

            Assert.Equal(3.0, vertex.Position.Z);
            Assert.Equal(3.0, appended.Position.Z);
            Assert.NotNull(appended.Handle);
            Assert.Same(appended, polyline.Last());

            vertex.Erase();

            Assert.True(vertex.IsErased);
            Assert.Equal(3, polyline.Count());
            Assert.Throws<InvalidOperationException>(() => vertex.Erase());
            Assert.Throws<InvalidOperationException>(() => vertex.Bulge = 1.0);
            Assert.Equal(Bits(new GeoPoint3(5, 0, 0)), Bits(vertex.Position));
        }

        [Fact]
        public void AVertexsLookAndData_AreSavedInItsOwnRecord_AndAVertexPutInKeepsTheOthersHandles()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");
            DxfVertex2d vertex = polyline.ElementAt(1);

            Assert.Same(polyline, vertex.Owner);
            Assert.Equal("49", vertex.OwnerHandle);
            vertex.ColorIndex = 3;
            vertex.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "B-12") };

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "4C");
            DxfVertex2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(saved), "49").ElementAt(1);
            Assert.Equal(3, again.ColorIndex);
            Assert.Equal("B-12", again.GetXDataForApplication("GHPROBE")[1].Value);

            polyline.InsertVertexAt(polyline.First(), new DxfVertex2d(new GeoPoint3(2.5, -1, 0), 0.0, 0.0, 0.0, 0.0));
            polyline.ElementAt(2).Erase();
            Assert.Equal(new[] { "4B", polyline.ElementAt(1).Handle, "4D" }, polyline.Select(v => v.Handle).ToArray());
            Assert.True(vertex.IsErased);
        }

        [Fact]
        public void AVertexMoved_StaysInThePlaneOfItsPolyline()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");
            DxfVertex2d vertex = polyline.ElementAt(1);

            vertex.TransformBy(GeoTransform3.Identity.Multiply(new GeoTransform3(new double[,] { { 1, 0, 0, 2 }, { 0, 1, 0, 3 }, { 0, 0, 1, 4 }, { 0, 0, 0, 1 } })));

            Assert.Equal(Bits(new GeoPoint3(7, 3, 0)), Bits(vertex.Position));
        }
    }
}
