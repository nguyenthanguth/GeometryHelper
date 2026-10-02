using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Grids: whole cells where the material holds them, cut ones along the boundary and the holes, joints between, and the
    /// grid standing where the alignments or an origin say.
    /// </summary>
    public class GridMeshTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        private static int Whole(GeoMesh2 mesh) => Enumerable.Range(0, mesh.FaceCount).Count(mesh.IsWhole);

        [Fact]
        public void AWallLaysWholePanelsWhereItDividesByThem()
        {
            GeoPolygon2 wall = Box(0, 0, 6000, 3000);
            GeoMesh2 mesh = wall.ToMesh(MeshOptions.Grid(600, 300), Tolerance);

            Assert.Equal(MeshKind.Grid, mesh.Kind);
            Assert.Equal(100, mesh.FaceCount);
            Assert.Equal(100, Whole(mesh));
            Assert.Equal(11 * 11, mesh.VertexCount);
            MeshAssert.IsSound(mesh, p => wall.Locate(p, Tolerance), 6000 * 3000, Tolerance);

            foreach (GeoPolygon2 face in mesh.GetFaces())
            {
                Assert.Equal(4, face.VertexCount);
                Assert.Equal(600 * 300, face.Area, 6);
            }
        }

        [Fact]
        public void WhatIsLeftOverIsCutOffAtTheFarSide()
        {
            // 1000 by 500 in cells of 300 by 200: three whole columns and 100 left over, two whole rows and 100 left over.
            GeoPolygon2 plate = Box(0, 0, 1000, 500);
            GeoMesh2 mesh = plate.ToMesh(MeshOptions.Grid(300, 200), Tolerance);

            Assert.Equal(12, mesh.FaceCount);
            Assert.Equal(6, Whole(mesh));
            MeshAssert.IsSound(mesh, p => plate.Locate(p, Tolerance), 1000 * 500, Tolerance);

            double[] widths = mesh.GetFaces().Select(f => f.Vertices.Max(v => v.X) - f.Vertices.Min(v => v.X)).Distinct().OrderBy(w => w).ToArray();
            Assert.Equal(new[] { 100.0, 300.0 }, widths);
            Assert.Contains(mesh.GetFaces(), f => f.Vertices.Min(v => v.X) == 900.0 && f.Vertices.Max(v => v.X) == 1000.0);
        }

        [Theory]
        [InlineData(GridAlignment.Start, 0.0, 300.0)]
        [InlineData(GridAlignment.End, 100.0, 400.0)]
        [InlineData(GridAlignment.CenterCell, 50.0, 350.0)]
        [InlineData(GridAlignment.CenterJoint, 200.0, 500.0)]
        public void TheAlignmentSaysWhereTheCutCellsGo(GridAlignment alignment, double firstWholeStart, double firstWholeEnd)
        {
            // 1000 wide in cells of 300: where the first whole cell from the left starts says where the grid stands.
            GeoPolygon2 strip = Box(0, 0, 1000, 100);
            GeoMesh2 mesh = strip.ToMesh(MeshOptions.Grid(300, 100, 0.0, alignment), Tolerance);

            MeshAssert.IsSound(mesh, p => strip.Locate(p, Tolerance), 1000 * 100, Tolerance);

            GeoPolygon2 first = Enumerable.Range(0, mesh.FaceCount).Where(mesh.IsWhole).Select(mesh.GetFace).OrderBy(f => f.Vertices.Min(v => v.X)).First();
            Assert.Equal(firstWholeStart, first.Vertices.Min(v => v.X), 9);
            Assert.Equal(firstWholeEnd, first.Vertices.Max(v => v.X), 9);

            // The centred ones leave the same cut at both ends.
            double[] widths = mesh.GetFaces().OrderBy(f => f.Vertices.Min(v => v.X)).Select(f => f.Vertices.Max(v => v.X) - f.Vertices.Min(v => v.X)).ToArray();

            if (alignment == GridAlignment.CenterCell || alignment == GridAlignment.CenterJoint)
            {
                Assert.Equal(widths[0], widths[widths.Length - 1], 9);
            }
        }

        [Theory]
        [InlineData(GridAlignment.CenterCell, 350.0, 650.0)]
        [InlineData(GridAlignment.CenterJoint, 505.0, 805.0)]
        public void TheCentredAlignmentsCountTheJoints(GridAlignment alignment, double middleStart, double middleEnd)
        {
            // 1000 wide in cells of 300 a joint of 10 apart: a cell on the middle, or the joint on it, from 495 to 505.
            GeoPolygon2 strip = Box(0, 0, 1000, 100);
            GeoMesh2 mesh = strip.ToMesh(MeshOptions.Grid(300, 100, 10.0, alignment), Tolerance);

            MeshAssert.IsSound(mesh, p => strip.Locate(p, Tolerance), mesh.Area, Tolerance, covers: false);

            GeoPolygon2[] cells = mesh.GetFaces().OrderBy(f => f.Vertices.Min(v => v.X)).ToArray();
            Assert.Contains(cells, f => Math.Abs(f.Vertices.Min(v => v.X) - middleStart) < 1E-9 && Math.Abs(f.Vertices.Max(v => v.X) - middleEnd) < 1E-9);

            double first = cells[0].Vertices.Max(v => v.X) - cells[0].Vertices.Min(v => v.X);
            double last = cells[cells.Length - 1].Vertices.Max(v => v.X) - cells[cells.Length - 1].Vertices.Min(v => v.X);
            Assert.Equal(first, last, 9);
        }

        [Fact]
        public void JointsStandBetweenTheCellsAndNotAtTheBoundary()
        {
            // Panels of 1200 by 600 with joints of 3: the pitch is 1203 by 603.
            GeoPolygon2 wall = Box(0, 0, 3609, 1206);
            GeoMesh2 mesh = wall.ToMesh(MeshOptions.Grid(1200, 600, 3.0), Tolerance);

            // Three columns and two rows of whole panels: the next would start on the far side, a joint past the last.
            Assert.Equal(6, mesh.FaceCount);
            Assert.Equal(6, Whole(mesh));
            Assert.All(mesh.GetFaces(), f => Assert.Equal(1200 * 600, f.Area, 6));
            MeshAssert.IsSound(mesh, p => wall.Locate(p, Tolerance), 6 * 1200 * 600, Tolerance, covers: false);

            // Nothing touches across a joint.
            Assert.All(Enumerable.Range(0, mesh.FaceCount), f => Assert.Empty(mesh.GetAdjacentFaces(f)));

            // The panels stand a joint apart.
            double[] starts = mesh.GetFaces().Select(f => f.Vertices.Min(v => v.X)).Distinct().OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 0.0, 1203.0, 2406.0 }, starts);
        }

        [Fact]
        public void ACellOverTheBoundaryIsCutToItAndAJointToo()
        {
            // An L with joints: the cut cells meet the boundary with no joint against it.
            var l = new GeoPolygon2(P(0, 0), P(2000, 0), P(2000, 1000), P(1000, 1000), P(1000, 2000), P(0, 2000));
            GeoMesh2 mesh = l.ToMesh(MeshOptions.Grid(450, 450, 10.0), Tolerance);

            // Cells start every 460 and are 450 wide, the last cut to 160 by the side at 2000: 1960 of every 2000 across
            // and up is cell. The L is the square less its upper right quarter, where the cells cover 370 + 450 + 160.
            const double covered = 1960.0 * 1960.0 - 980.0 * 980.0;
            MeshAssert.IsSound(mesh, p => l.Locate(p, Tolerance), covered, Tolerance, covers: false);

            // Five by five cells over the L's box, the four wholly in its missing quarter left out.
            Assert.Equal(21, mesh.FaceCount);
            Assert.Equal(12, Enumerable.Range(0, mesh.FaceCount).Count(mesh.IsWhole));
        }

        [Fact]
        public void ACellHoldingAHoleWholeIsSplitThroughIt()
        {
            // A hole of 100 square in the middle of one cell of 600: that cell comes back in two, neither with a hole.
            var face = new GeoFace2(Box(0, 0, 1800, 600), new[] { Box(850, 250, 950, 350) });
            GeoMesh2 mesh = face.ToMesh(MeshOptions.Grid(600, 600), Tolerance);

            MeshAssert.IsSound(mesh, p => face.Locate(p, Tolerance), 1800 * 600 - 100 * 100, Tolerance);
            Assert.Equal(4, mesh.FaceCount);
            Assert.Equal(2, Whole(mesh));

            // The split runs through the middle of the hole, so the cells either side take its new corners on their sides.
            GeoPolygon2[] cut = Enumerable.Range(0, mesh.FaceCount).Where(f => !mesh.IsWhole(f)).Select(mesh.GetFace).ToArray();
            Assert.All(cut, f => Assert.Equal(600 * 600 / 2.0 - 100 * 100 / 2.0, f.Area, 6));
        }

        [Fact]
        public void HolesAcrossTheGridLinesAreCutAroundAndTouchingOnesToo()
        {
            var face = new GeoFace2(
                Box(0, 0, 2000, 1500),
                new[]
                {
                    Box(500, 500, 700, 700),           // across a vertical and a horizontal line of a 600 grid
                    Box(1150, 100, 1250, 400),         // whole in a cell
                    Box(1250, 100, 1350, 400),         // touching the one before
                    Box(1700, 1300, 2000, 1500),       // in the corner, touching the boundary
                });
            GeoMesh2 mesh = face.ToMesh(MeshOptions.Grid(600, 600), Tolerance);

            MeshAssert.IsSound(mesh, p => face.Locate(p, Tolerance), face.Area, Tolerance);
        }

        [Fact]
        public void AnOriginPutsACellCornerOnIt()
        {
            GeoPolygon2 plate = Box(0, 0, 1000, 1000);
            GeoMesh2 mesh = plate.ToMesh(MeshOptions.Grid(400, 400, P(100, 150)), Tolerance);

            MeshAssert.IsSound(mesh, p => plate.Locate(p, Tolerance), 1000 * 1000, Tolerance);
            Assert.Contains(mesh.Vertices, v => v.IsEqualTo(P(100, 150), new Tolerance(1E-9, 1E-9)));
            Assert.Contains(mesh.Vertices, v => v.IsEqualTo(P(500, 550), new Tolerance(1E-9, 1E-9)));

            // Cells reach both ways from it: 100 + 2 x 400 + 100 across, 150 + 2 x 400 + 50 up.
            Assert.Equal(16, mesh.FaceCount);
            Assert.Equal(4, Whole(mesh));
        }

        [Fact]
        public void AGridTurnedByAnAngleRunsThatWay()
        {
            GeoPolygon2 plate = Box(0, 0, 3000, 2000);
            double angle = Math.PI / 6;
            GeoMesh2 mesh = plate.ToMesh(new MeshOptions(MeshKind.Grid, 500, 250, angleRad: angle), Tolerance);

            MeshAssert.IsSound(mesh, p => plate.Locate(p, Tolerance), 3000 * 2000, Tolerance);

            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));

            foreach (int f in Enumerable.Range(0, mesh.FaceCount).Where(mesh.IsWhole))
            {
                GeoPolygon2 cell = mesh.GetFace(f);
                var corners = MeshAssert.Turns(cell, 1E-6);
                Assert.Equal(4, corners.Count);
                Assert.Equal(500 * 250, cell.Area, 4);

                // Its sides run along the grid's axes.
                for (int i = 0; i < 4; i++)
                {
                    GeoVector2 side = corners[i].GetVectorTo(corners[(i + 1) % 4]).Normalize();
                    double cross = Math.Abs(side.CrossProduct(along));
                    Assert.True(cross < 1E-9 || Math.Abs(cross - 1.0) < 1E-9, $"a side runs at {cross} to the axis");
                }
            }

            Assert.True(Whole(mesh) > 20);
        }

        [Fact]
        public void ATurnedRectangleIsLaidAlongItsOwnSides()
        {
            var rectangle = new GeoRectangle2(P(5000, -2000), 2400, 1200, 0.7);
            GeoMesh2 mesh = rectangle.ToMesh(MeshOptions.Grid(600, 400), Tolerance);

            Assert.Equal(12, mesh.FaceCount);
            Assert.Equal(12, Whole(mesh));
            MeshAssert.IsSound(mesh, p => rectangle.Locate(p, Tolerance), 2400 * 1200, Tolerance);

            // The same cells Divide gives.
            GeoRectangle2[] divided = rectangle.Divide(4, 3);
            Assert.All(divided, cell => Assert.Contains(mesh.GetFaces(), face => face.Vertices.All(v => cell.GetVertices().Any(c => c.IsEqualTo(v, new Tolerance(1E-7, 1E-7))))));
        }

        [Fact]
        public void ADiscIsCutToItsRim()
        {
            var disc = new GeoCircle2(P(100, 200), 1000);
            var options = new MeshOptions(MeshKind.Grid, 300, 300, chordTolerance: 0.5);
            GeoMesh2 mesh = disc.ToMesh(options, Tolerance);
            GeoPolygon2 rim = disc.ToPolygonByChordTolerance(0.5);

            MeshAssert.IsSound(mesh, p => rim.Locate(p, Tolerance), rim.Area, Tolerance);
        }

        [Fact]
        public void ALoopWithArcsIsFlattenedFirst()
        {
            GeoPolygonArc2 rounded = Box(0, 0, 2000, 1000).Fillet(300);
            GeoMesh2 mesh = rounded.ToMesh(new MeshOptions(MeshKind.Grid, 250, 250, chordTolerance: 0.1), Tolerance);
            GeoPolygon2 flat = rounded.Flatten(0.1);

            MeshAssert.IsSound(mesh, p => flat.Locate(p, Tolerance), flat.Area, Tolerance);
        }

        [Fact]
        public void FarFromTheOriginTheCellsAreAsExact()
        {
            GeoPolygon2 plate = Box(512345.25, 7012345.5, 512345.25 + 3000, 7012345.5 + 1500);
            GeoMesh2 mesh = plate.ToMesh(MeshOptions.Grid(500, 500), Tolerance);

            Assert.Equal(18, mesh.FaceCount);
            Assert.Equal(18, Whole(mesh));
            MeshAssert.IsSound(mesh, p => plate.Locate(p, Tolerance), 3000 * 1500, Tolerance);
        }

        [Fact]
        public void TheShapesOwnCornersAreKeptExactly()
        {
            var shape = new GeoPolygon2(P(0.1, 0.2), P(1234.567, 3.21), P(1500.3, 987.6), P(321.123, 1111.1));
            GeoMesh2 mesh = shape.ToMesh(new MeshOptions(MeshKind.Grid, 200, 150, angleRad: 0.3), Tolerance);

            MeshAssert.IsSound(mesh, p => shape.Locate(p, Tolerance), shape.Area, Tolerance);

            foreach (GeoPoint2 corner in shape.Vertices)
            {
                Assert.Contains(corner, mesh.Vertices);
            }
        }

        [Fact]
        public void AnOriginFarFromTheShapeLeavesItsCornersExact()
        {
            // Laid out from an origin far off, a corner comes back from the cutting a few digits short; it is the shape's
            // own corner again in the mesh.
            var shape = new GeoPolygon2(P(0.1, 0.7), P(1000.3, 0.9), P(1100.7, 800.3), P(0.3, 900.1));
            GeoMesh2 mesh = shape.ToMesh(MeshOptions.Grid(300, 300, P(-98765.4321, 123456.789)), Tolerance);

            MeshAssert.IsSound(mesh, p => shape.Locate(p, Tolerance), shape.Area, Tolerance);

            foreach (GeoPoint2 corner in shape.Vertices)
            {
                Assert.Contains(corner, mesh.Vertices);
            }
        }

        [Fact]
        public void ANeckNarrowerThanThePointToleranceIsKeptAsItIs()
        {
            // An hourglass whose waist is 0.006 across, in one cell: the material it is, every corner kept, in one face.
            var hourglass = new GeoPolygon2(P(0, 0), P(200, 0), P(100.003, 100), P(200, 200), P(0, 200), P(99.997, 100));
            GeoMesh2 mesh = hourglass.ToMesh(MeshOptions.Grid(300, 300), Tolerance);

            Assert.Equal(1, mesh.FaceCount);
            Assert.Equal(6, mesh.GetFace(0).VertexCount);
            Assert.Equal(hourglass.Area, mesh.Area, 9);
            MeshAssert.EdgeToEdge(mesh, Tolerance);
        }

        [Fact]
        public void HolesTouchingCornerToSideLeaveTheMaterialBetweenThemInTwo()
        {
            // The corner of a diamond on the side of a box, both holes, in one cell: the material between them is pinched to
            // that point, and the cell comes back as the faces either side of it, not as one face touching itself there.
            var face = new GeoFace2(
                Box(0, 0, 1000, 1000),
                new[] { new GeoPolygon2(P(300, 500), P(400, 400), P(500, 500), P(400, 600)), Box(500, 450, 700, 650) });
            GeoMesh2 mesh = face.ToMesh(MeshOptions.Grid(2000, 2000), Tolerance);

            MeshAssert.IsSound(mesh, p => face.Locate(p, Tolerance), face.Area, Tolerance);
            Assert.True(mesh.FaceCount >= 2, $"{mesh.FaceCount} faces");
        }

        [Fact]
        public void AGridNeedsCellsLargerThanThePointToleranceAndNotTooMany()
        {
            GeoPolygon2 plate = Box(0, 0, 100000, 100000);

            Assert.Throws<ArgumentException>(() => plate.ToMesh(MeshOptions.Grid(0.005, 10), Tolerance));
            Assert.Throws<ArgumentException>(() => plate.ToMesh(MeshOptions.Grid(10, 10), Tolerance));
            Assert.Throws<ArgumentException>(() => plate.ToMesh(MeshKind.Grid));

            // At a tolerance of nought, cells of 1E-14 along 100 000, more than 2^53 of them: the steps that find the first
            // cell never ended, as a cell's number less one is the number itself there.
            Assert.Throws<ArgumentException>(() => plate.ToMesh(MeshOptions.Grid(1E-14, 1E-14), new Tolerance(0.0, 0.0, 0.0, 0.0)));
        }

        [Fact]
        public void NoAreaIsNoFaces()
        {
            GeoMesh2 flat = new GeoPolygon2(P(0, 0), P(100, 0), P(200, 0)).ToMesh(MeshOptions.Grid(50, 50), Tolerance);
            Assert.Equal(0, flat.FaceCount);
            Assert.Equal(0, flat.VertexCount);
            Assert.Equal(0.0, flat.Area);

            Assert.Equal(0, new GeoRectangle2(P(0, 0), 100, 0).ToMesh(MeshOptions.Grid(50, 50), Tolerance).FaceCount);
            Assert.Equal(0, new GeoCircle2(P(0, 0), 0).ToMesh(MeshOptions.Grid(50, 50), Tolerance).FaceCount);
        }
    }
}
