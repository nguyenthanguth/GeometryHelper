using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// The mesh itself: its vertices and faces, edges and neighbours, triangles, and moves.
    /// </summary>
    public class GeoMesh2Tests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        /// <summary>Three by three cells of 100.</summary>
        private static GeoMesh2 Grid3() => Box(0, 0, 300, 300).ToMesh(MeshOptions.Grid(100, 100), Tolerance);

        [Fact]
        public void CellsSideBySideShareTheirCornersAndEdges()
        {
            GeoMesh2 mesh = Grid3();

            Assert.Equal(9, mesh.FaceCount);
            Assert.Equal(16, mesh.VertexCount);
            Assert.Equal(24, mesh.GetEdges().Length);
            Assert.Equal(12, mesh.GetBoundaryEdges().Length);
            Assert.Equal(90000.0, mesh.Area, 9);
        }

        [Fact]
        public void TheBoundaryEdgesRunWithTheMaterialOnTheirLeft()
        {
            GeoMesh2 mesh = Grid3();

            foreach (GeoLine2 edge in mesh.GetBoundaryEdges())
            {
                GeoVector2 along = edge.Direction.Normalize();
                GeoPoint2 left = edge.MidPoint.Add(new GeoVector2(-along.Y, along.X).Multiply(1.0));
                Assert.Equal(GeometryHelper.Enums.PointLocation.Inside, Box(0, 0, 300, 300).Locate(left, Tolerance));
            }
        }

        [Fact]
        public void TheNeighboursOfACellAreTheCellsSharingASide()
        {
            GeoMesh2 mesh = Grid3();
            int middle = Enumerable.Range(0, mesh.FaceCount).Single(f => mesh.GetFace(f).Locate(P(150, 150), Tolerance) == GeometryHelper.Enums.PointLocation.Inside);
            int corner = Enumerable.Range(0, mesh.FaceCount).Single(f => mesh.GetFace(f).Locate(P(50, 50), Tolerance) == GeometryHelper.Enums.PointLocation.Inside);

            Assert.Equal(4, mesh.GetAdjacentFaces(middle).Length);
            Assert.Equal(2, mesh.GetAdjacentFaces(corner).Length);
            Assert.Contains(middle, mesh.GetAdjacentFaces(mesh.GetAdjacentFaces(middle)[0]));

            int[] neighbours = mesh.GetAdjacentFaces(middle);
            Assert.Equal(neighbours.OrderBy(n => n), neighbours);
        }

        [Fact]
        public void TheTrianglesOfTheFacesCoverThemOnTheirOwnCorners()
        {
            // An L in strips: the lower band has a corner on its top side, so it is five corners in a row-broken rectangle.
            var l = new GeoPolygon2(P(0, 0), P(2000, 0), P(2000, 1000), P(1000, 1000), P(1000, 2000), P(0, 2000));

            foreach (GeoMesh2 mesh in new[] { l.ToMesh(MeshKind.Strips), l.ToMesh(MeshKind.Convex), l.ToMesh(MeshOptions.Grid(300, 300), Tolerance) })
            {
                GeoTriangle2[] triangles = mesh.ToTriangles();

                Assert.Equal(mesh.Area, triangles.Sum(t => t.SignedArea), 6);
                Assert.All(triangles, t => Assert.True(t.SignedArea > 0.0));
                Assert.All(triangles.SelectMany(t => new[] { t.A, t.B, t.C }), v => Assert.Contains(v, mesh.Vertices));

                // Every corner of the mesh is a corner of some triangle: a face's corners in a row are not left out.
                Assert.All(mesh.Vertices, v => Assert.Contains(triangles, t => t.A == v || t.B == v || t.C == v));
            }
        }

        [Fact]
        public void TheFacesAndTheirIndexesAreCopies()
        {
            GeoMesh2 mesh = Grid3();
            int[] indices = mesh.GetFaceIndices(0);
            indices[0] = 99;

            Assert.NotEqual(99, mesh.GetFaceIndices(0)[0]);
            Assert.Equal(mesh.GetFace(0), mesh.GetFaces()[0]);

            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.GetFace(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.GetFaceIndices(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.IsWhole(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.GetAdjacentFaces(9));
        }

        [Fact]
        public void MovingAMeshMovesItsVerticesAndKeepsItsFaces()
        {
            GeoMesh2 mesh = Grid3();

            GeoMesh2 moved = mesh.Translate(new GeoVector2(1000, -500));
            Assert.Equal(mesh.Vertices.Select(v => v.Add(new GeoVector2(1000, -500))), moved.Vertices);
            Assert.Equal(mesh.FaceCount, moved.FaceCount);
            Assert.Equal(mesh.Area, moved.Area, 9);
            Assert.True(moved.IsWhole(0));

            GeoMesh2 turned = mesh.RotateBy(Math.PI / 2, P(0, 0));
            Assert.Equal(mesh.Area, turned.Area, 6);
            Assert.All(turned.GetFaces(), f => Assert.True(f.SignedArea > 0.0));
            Assert.True(turned.Vertices.All(v => v.X <= 1E-9 && v.Y >= -1E-9));

            // A mirror turns each face round, so that it still runs counter-clockwise.
            GeoMesh2 mirrored = mesh.TransformBy(GeoTransform2.Mirror(new GeoLine2(P(0, 0), P(0, 1))));
            Assert.Equal(mesh.Area, mirrored.Area, 6);
            Assert.All(mirrored.GetFaces(), f => Assert.True(f.SignedArea > 0.0));
            MeshAssert.EdgeToEdge(mirrored, Tolerance);

            Assert.Throws<ArgumentNullException>(() => mesh.TransformBy(null));
        }

        [Fact]
        public void ItSaysWhatItIs()
        {
            Assert.Equal("GeoMesh2[Grid, Vertices: 16, Faces: 9, Area: 90000]", Grid3().ToString());
        }
    }
}
