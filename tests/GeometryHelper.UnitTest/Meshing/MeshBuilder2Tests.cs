using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Joining pieces into a mesh, asked directly: what each step does to pieces made to need it.
    /// </summary>
    public class MeshBuilder2Tests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static MeshBuilder2 Builder() => new MeshBuilder2(Tolerance, 1000.0, 1000.0);

        [Fact]
        public void APieceRunningThroughAPlaceTwiceIsTheLoopsEitherSide()
        {
            // A figure of eight through (100, 100): two squares meeting at a corner, as one loop.
            MeshBuilder2 builder = Builder();
            builder.Add(new[] { P(0, 0), P(100, 0), P(100, 100), P(200, 100), P(200, 200), P(100, 200), P(100, 100), P(0, 100) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Grid);

            Assert.Equal(2, mesh.FaceCount);
            Assert.All(mesh.GetFaces(), f => Assert.Equal(10000.0, f.Area, 9));
        }

        [Fact]
        public void APieceTouchingASideOfItsOwnIsTheLoopsEitherSide()
        {
            // A square with a notch whose tip touches the far side at (50, 0): the material either side meets only there.
            MeshBuilder2 builder = Builder();
            builder.Add(new[] { P(0, 0), P(100, 0), P(100, 100), P(60, 100), P(50, 0.0), P(40, 100), P(0, 100) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Grid);

            Assert.Equal(2, mesh.FaceCount);
            Assert.Equal(new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(60, 100), P(50, 0.0), P(40, 100), P(0, 100)).Area, mesh.Area, 9);
            Assert.All(mesh.GetFaces(), f => Assert.True(f.IsSimple(new Tolerance(1E-9, 1E-9))));
        }

        [Fact]
        public void ACornerOfAFaceAcrossASideIsPutOnIt()
        {
            // A square beside two that share its side between them: its side takes the corner they meet at.
            MeshBuilder2 builder = Builder();
            builder.Add(new[] { P(0, 0), P(100, 0), P(100, 100), P(0, 100) }, true);
            builder.Add(new[] { P(100, 0), P(200, 0), P(200, 50), P(100, 50) }, false);
            builder.Add(new[] { P(100, 50), P(200, 50), P(200, 100), P(100, 100) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Grid);

            Assert.Equal(5, mesh.GetFace(0).VertexCount);
            Assert.Contains(P(100, 50), mesh.GetFace(0).Vertices);
            Assert.True(mesh.IsWhole(0));
            MeshAssert.EdgeToEdge(mesh, Tolerance);
        }

        [Fact]
        public void ATriangleGivenACornerOnASideIsSplitThere()
        {
            // Two triangles below the first one's base meet at its middle.
            MeshBuilder2 builder = Builder();
            builder.Add(new[] { P(0, 0), P(100, 0), P(0, 100) }, false);
            builder.Add(new[] { P(0, 0), P(50, -50), P(50, 0) }, false);
            builder.Add(new[] { P(50, 0), P(50, -50), P(100, 0) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Triangles);

            Assert.Equal(4, mesh.FaceCount);
            Assert.All(mesh.GetFaces(), f => Assert.Equal(3, f.VertexCount));
            Assert.Equal(5000.0 + 2500.0, mesh.Area, 9);
            MeshAssert.EdgeToEdge(mesh, Tolerance);
        }

        [Fact]
        public void AsConvexPiecesTwoTrianglesOfASquareAreOne()
        {
            MeshBuilder2 builder = Builder();
            builder.Add(new[] { P(0, 0), P(100, 0), P(100, 100) }, false);
            builder.Add(new[] { P(0, 0), P(100, 100), P(0, 100) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Convex);

            Assert.Equal(1, mesh.FaceCount);
            Assert.Equal(4, mesh.GetFace(0).VertexCount);
        }

        [Fact]
        public void PointsTheSameButForRoundingAreOneVertexAndNearerOnesAreNot()
        {
            MeshBuilder2 builder = Builder();
            builder.Seed(new[] { P(100, 0) });
            builder.Add(new[] { P(0, 0), P(100.000000001, 0), P(100, 100) }, false);
            builder.Add(new[] { P(100, 0), P(200, 0), P(100.005, 100) }, false);
            GeoMesh2 mesh = builder.Build(MeshKind.Grid);

            // The seed stands for the point a nanometre off it; a point five micrometres off another is a vertex of its own.
            Assert.Contains(P(100, 0), mesh.Vertices);
            Assert.DoesNotContain(P(100.000000001, 0), mesh.Vertices);
            Assert.Contains(P(100.005, 100), mesh.Vertices);
            Assert.Contains(P(100, 100), mesh.Vertices);
            Assert.Equal(5, mesh.VertexCount);
        }
    }
}
