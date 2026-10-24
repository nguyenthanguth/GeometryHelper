using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The polygon mesh as a <see cref="DxfPolygonMesh"/>: made in code for R12 to R2018, its grid, flags and points read back to
    /// the bit, the body the reader makes of it, and moved point by point.
    /// </summary>
    public class DxfPolygonMeshTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        /// <summary>A grid of 3 rows of 4 points, closed in N: a tube of 8 cells.</summary>
        private static DxfPolygonMesh Tube() => new DxfPolygonMesh(
            DxfPolyMeshType.SimpleMesh,
            3,
            4,
            Enumerable.Range(0, 12).Select(i => new GeoPoint3(i / 4, Math.Cos(i % 4 * Math.PI / 2), Math.Sin(i % 4 * Math.PI / 2))),
            false,
            true);

        [Fact]
        public void AMeshMadeInCode_IsAppendedToEachVersion_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfPolygonMesh mesh = Tube();
                mesh.Layer = "pipes";

                db.ModelSpace.AppendEntity(mesh);

                DxfPolygonMesh again = ByHandle<DxfPolygonMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
                Assert.Equal(3, again.MSize);
                Assert.Equal(4, again.NSize);
                Assert.False(again.IsMClosed);
                Assert.True(again.IsNClosed);
                Assert.Equal(DxfPolyMeshType.SimpleMesh, again.PolyMeshType);
                Assert.Equal(12, again.Count());
                Assert.Equal(Bits(new GeoPoint3(2, Math.Cos(Math.PI), Math.Sin(Math.PI))), Bits(again.ElementAt(10).Position));
                Assert.Equal(8, ((GeoSolid3)Assert.Single(again.GetGeometry())).Faces.Count);
                Assert.Equal("pipes", again.Layer);
            }

            Assert.Throws<ArgumentException>(() => new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 3, 3, new[] { new GeoPoint3(0, 0, 0) }, false, false));
            Assert.Throws<NotSupportedException>(() => new DxfPolygonMesh(DxfPolyMeshType.CubicSurfaceMesh, 2, 2, new GeoPoint3[4], false, false));
        }

        [Fact]
        public void TheGridsFlagsAndSizesSet_AreTheHeadWrittenAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolygonMesh mesh = Tube();
            db.ModelSpace.AppendEntity(mesh);

            mesh.MakeMClosed();
            mesh.MakeNOpen();
            mesh.MSurfaceDensity = 6;
            mesh.NSurfaceDensity = 7;
            mesh.MakeMClosed();

            DxfPolygonMesh again = ByHandle<DxfPolygonMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
            Assert.True(again.IsMClosed);
            Assert.False(again.IsNClosed);
            Assert.Equal(6, again.MSurfaceDensity);
            Assert.Equal(7, again.NSurfaceDensity);
        }

        [Fact]
        public void AMeshTurned_HasEachPointMoved_ItsHandleKept()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolygonMesh tube = Tube();
            made.ModelSpace.AppendEntity(tube);
            string text = made.ToText();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolygonMesh mesh = ByHandle<DxfPolygonMesh>(db, tube.Handle);
            string[] handles = mesh.Select(v => v.Handle).ToArray();

            mesh.TransformBy(DxfPolylineTests.Turn());

            OnlyTheseChanged(text, db.ToText(), handles);
            DxfPolygonMesh again = ByHandle<DxfPolygonMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
            Assert.Equal(Bits(DxfPolylineTests.Turn().Transform(new GeoPoint3(1, Math.Cos(Math.PI / 2), Math.Sin(Math.PI / 2)))), Bits(again.ElementAt(5).Position));
        }
    }
}
