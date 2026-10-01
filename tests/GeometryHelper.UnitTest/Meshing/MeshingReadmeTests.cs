using System;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Runs the code shown in docs/mesh.md and checks what it says, so that renaming or removing an API breaks the build
    /// rather than leaving a reader with instructions that cannot work.
    /// </summary>
    public class MeshingReadmeTests
    {
        private readonly ITestOutputHelper _output;

        public MeshingReadmeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        [Fact]
        public void QuickStart_PanelsOfFormworkOnAWallWithADoorAndAWindow()
        {
            // Six panels of 1200 and five of 600 fit it with joints of 3 between them: 6 x 1203 - 3 by 5 x 603 - 3.
            var wall = new GeoFace2(
                Box(0, 0, 7215, 3012),
                new[]
                {
                    Box(900, 0, 1800, 2100),      // a door
                    Box(3600, 900, 5400, 2100),   // a window
                });

            GeoMesh2 panels = wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3));

            int whole = Enumerable.Range(0, panels.FaceCount).Count(panels.IsWhole);
            int cut = panels.FaceCount - whole;

            _output.WriteLine($"{panels.FaceCount} panels, {whole} whole, {cut} cut, area {panels.Area}");
            Assert.Equal(29, panels.FaceCount);
            Assert.Equal(13, whole);
            Assert.Equal(16, cut);
        }

        [Fact]
        public void TheGrid_TheAlignmentsSayWhereTheCutCellsGo()
        {
            GeoPolygon2 strip = Box(0, 0, 1000, 100);

            foreach (GridAlignment alignment in new[] { GridAlignment.Start, GridAlignment.End, GridAlignment.CenterCell, GridAlignment.CenterJoint })
            {
                GeoMesh2 mesh = strip.ToMesh(MeshOptions.Grid(300, 100, 0, alignment));
                double[] widths = mesh.GetFaces()
                    .Select(f => f.Vertices.Max(v => v.X) - f.Vertices.Min(v => v.X))
                    .Select(w => Math.Round(w, 6))
                    .OrderBy(w => w).ToArray();

                _output.WriteLine($"{alignment}: {string.Join(", ", widths)}");
            }

            double[] Widths(GridAlignment a) => strip.ToMesh(MeshOptions.Grid(300, 100, 0, a)).GetFaces()
                .OrderBy(f => f.Vertices.Min(v => v.X))
                .Select(f => Math.Round(f.Vertices.Max(v => v.X) - f.Vertices.Min(v => v.X), 6)).ToArray();

            Assert.Equal(new[] { 300.0, 300.0, 300.0, 100.0 }, Widths(GridAlignment.Start));
            Assert.Equal(new[] { 100.0, 300.0, 300.0, 300.0 }, Widths(GridAlignment.End));
            Assert.Equal(new[] { 50.0, 300.0, 300.0, 300.0, 50.0 }, Widths(GridAlignment.CenterCell));
            Assert.Equal(new[] { 200.0, 300.0, 300.0, 200.0 }, Widths(GridAlignment.CenterJoint));
        }

        [Fact]
        public void TheGrid_AnOriginAndAnAngle()
        {
            GeoPolygon2 slab = Box(0, 0, 8000, 5000);

            // A cell's corner on a column at (2400, 1800), the grid turned 30 degrees.
            var options = new MeshOptions(MeshKind.Grid, 1500, 1500, angleRad: Math.PI / 6, origin: new GeoPoint2(2400, 1800));
            GeoMesh2 mesh = slab.ToMesh(options);

            Assert.Contains(new GeoPoint2(2400, 1800), mesh.Vertices);
            Assert.Equal(8000 * 5000, mesh.Area, 3);
        }

        [Fact]
        public void TheGrid_ARectangleAlongItsOwnSides()
        {
            var plate = new GeoRectangle2(new GeoPoint2(1000, 2000), 2400, 1200, Math.PI / 5);

            GeoMesh2 mesh = plate.ToMesh(MeshOptions.Grid(600, 400));
            GeoRectangle2[] cells = plate.Divide(4, 3);

            Assert.Equal(12, mesh.FaceCount);
            Assert.All(Enumerable.Range(0, 12), f => Assert.True(mesh.IsWhole(f)));
            Assert.Equal(12, cells.Length);
            Assert.Equal(600.0, cells[0].Width, 9);
        }

        [Fact]
        public void TheKinds_OneShapeFourWays()
        {
            var slab = new GeoFace2(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(9000, 0), new GeoPoint2(9000, 4000),
                                new GeoPoint2(4500, 4000), new GeoPoint2(4500, 8000), new GeoPoint2(0, 8000)),
                new[] { Box(1500, 1500, 2700, 2400), Box(1800, 5600, 3000, 6600) });

            GeoMesh2 triangles = slab.ToMesh(MeshKind.Triangles);
            GeoMesh2 convex = slab.ToMesh(MeshKind.Convex);
            GeoMesh2 strips = slab.ToMesh(MeshKind.Strips);          // along the X axis
            GeoMesh2 turned = slab.ToMesh(MeshOptions.Strips(Math.PI / 2));
            GeoMesh2 grid = slab.ToMesh(MeshOptions.Grid(1000, 1000));

            _output.WriteLine($"triangles {triangles.FaceCount}, convex {convex.FaceCount}, strips {strips.FaceCount}, turned {turned.FaceCount}, grid {grid.FaceCount}");
            Assert.Equal(16, triangles.FaceCount);
            Assert.Equal(9, convex.FaceCount);
            Assert.Equal(8, strips.FaceCount);
            Assert.Equal(8, turned.FaceCount);
            Assert.Equal(56, grid.FaceCount);

            foreach (GeoMesh2 mesh in new[] { triangles, convex, strips, turned, grid })
            {
                Assert.Equal(slab.Area, mesh.Area, 3);
            }
        }

        [Fact]
        public void TheMesh_WalkingItsFaces()
        {
            GeoMesh2 mesh = Box(0, 0, 3000, 2000).ToMesh(MeshOptions.Grid(1000, 1000));

            GeoPolygon2 first = mesh.GetFace(0);            // a polygon, counter-clockwise
            int[] corners = mesh.GetFaceIndices(0);         // its corners in mesh.Vertices
            int[] beside = mesh.GetAdjacentFaces(0);        // the faces sharing a side with it
            GeoLine2[] outline = mesh.GetBoundaryEdges();   // the material on the left of each
            GeoTriangle2[] triangles = mesh.ToTriangles();  // for a renderer or an OBJ file

            Assert.Equal(4, first.VertexCount);
            Assert.Equal(4, corners.Length);
            Assert.Equal(2, beside.Length);
            Assert.Equal(10, outline.Length);
            Assert.Equal(12, triangles.Length);
            Assert.Equal(12, mesh.VertexCount);
        }

        [Fact]
        public void Triangles_WithoutAMesh()
        {
            var face = new GeoFace2(Box(0, 0, 1000, 1000), new[] { Box(400, 400, 600, 600) });

            GeoTriangle2[] triangles = face.TriangulateSurface();

            Assert.Equal(1000 * 1000 - 200 * 200, triangles.Sum(t => t.Area), 6);
            Assert.All(triangles, t => Assert.False(t.IsClockwise));
        }
    }
}
