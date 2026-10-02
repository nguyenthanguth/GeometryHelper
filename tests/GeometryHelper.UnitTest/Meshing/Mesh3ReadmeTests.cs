using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Runs the code shown in docs/mesh3.md and checks what it says, so that renaming or removing an API breaks the build
    /// rather than leaving a reader with instructions that cannot work.
    /// </summary>
    public class Mesh3ReadmeTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>The rectangle with corners (x, 0, z), wound so that the wall faces -Y.</summary>
        private static GeoPolygon3 WallBox(double x0, double z0, double x1, double z1) => new GeoPolygon3(P(x0, 0, z0), P(x1, 0, z0), P(x1, 0, z1), P(x0, 0, z1));

        private static GeoSolid3 Prism(GeoPolygon2 plan, double z0, double z1)
            => GeoSolid3.Extrude(new GeoPolygon3(plan.Vertices.Select(p => P(p.X, p.Y, z0))), new GeoVector3(0, 0, z1 - z0));

        private static GeoPolygon2 Rect(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        private static GeoMesh3 Panels()
        {
            var wall = new GeoFace3(
                WallBox(0, 0, 7215, 3012),
                new[]
                {
                    WallBox(900, 0, 1800, 2100),      // a door
                    WallBox(3600, 900, 5400, 2100),   // a window
                });

            return wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3));
        }

        [Fact]
        public void FlatShapes_PanelsOnAWallStandingInSpace()
        {
            GeoMesh3 panels = Panels();

            int whole = Enumerable.Range(0, panels.FaceCount).Count(panels.IsWhole);
            GeoVector3 along = panels.Frame.XAxis;
            GeoVector3 up = panels.Frame.YAxis;

            Assert.Equal(29, panels.FaceCount);
            Assert.Equal(13, whole);
            Assert.True(along.IsEqualTo(new GeoVector3(1, 0, 0), new Tolerance(1E-12, 1E-12)));
            Assert.True(up.IsEqualTo(new GeoVector3(0, 0, 1), new Tolerance(1E-12, 1E-12)));
        }

        [Fact]
        public void WhichWayTheGridRuns_TilesOnARoof()
        {
            double run = 3000 * Math.Cos(Math.PI / 6);
            var roofSide = new GeoPolygon3(P(0, -run, 0), P(6000, -run, 0), P(6000, 0, 1500), P(0, 0, 1500));

            GeoMesh3 tiles = roofSide.ToMesh(MeshOptions.Grid(400, 300));   // 150 tiles, every one whole

            Assert.Equal(150, tiles.FaceCount);
            Assert.Equal(150, Enumerable.Range(0, tiles.FaceCount).Count(tiles.IsWhole));
            Assert.Equal(0.0, tiles.Frame.XAxis.Z, 12);
        }

        [Fact]
        public void WhichWayTheGridRuns_APlateLyingAskew()
        {
            GeoTransform3 askew = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7) * GeoTransform3.RotationZ(0.3);
            GeoPolygon3 plate = new GeoPolygon3(P(0, 0, 0), P(2400, 0, 0), P(2400, 1200, 0), P(0, 1200, 0)).TransformBy(askew);

            GeoMesh3 level = plate.ToMesh(MeshOptions.Grid(600, 400));                     // 21 faces, 3 whole
            GeoMesh3 own = plate.ToMesh(MeshOptions.Grid(600, 400), MeshPlacement3.Own);   // 12 faces, all whole

            Assert.Equal(21, level.FaceCount);
            Assert.Equal(3, Enumerable.Range(0, level.FaceCount).Count(level.IsWhole));
            Assert.Equal(12, own.FaceCount);
            Assert.Equal(12, Enumerable.Range(0, own.FaceCount).Count(own.IsWhole));
        }

        [Fact]
        public void WhichWayTheGridRuns_WallsLaidFromOneOrigin()
        {
            GeoPolygon3 frontWall = WallBox(0, 0, 5000, 2900);
            var sideWall = new GeoPolygon3(P(5000, 0, 0), P(5000, 4000, 0), P(5000, 4000, 2900), P(5000, 0, 2900));

            MeshPlacement3 placement = MeshPlacement3.World.At(new GeoPoint3(-3000, -2000, 150));
            GeoMesh3 front = frontWall.ToMesh(MeshOptions.Grid(1200, 600, joint: 10), placement);
            GeoMesh3 side = sideWall.ToMesh(MeshOptions.Grid(1200, 600, joint: 10), placement);

            // The rows stand at the same heights on both walls.
            double[] Heights(GeoMesh3 mesh) => mesh.Vertices.Select(v => Math.Round(v.Z, 6)).Distinct().OrderBy(z => z).ToArray();
            Assert.Equal(Heights(front), Heights(side));
            Assert.Throws<ArgumentException>(() => frontWall.ToMesh(MeshOptions.Grid(1200, 600, new GeoPoint2(10, 10))));
        }

        [Fact]
        public void OtherFlatShapes_ADiscAndASlot()
        {
            var circle = new GeoCircle3(P(0, 0, 0), new GeoVector3(1, -1, 2).Normalize(), 1500);
            Assert.Equal(50, circle.ToMesh(MeshKind.Triangles).FaceCount);

            var slot = new GeoPolygonArc3(
                new[] { P(0, 0, 0), P(2400, 0, 0), P(2400, 0, 600), P(0, 0, 600) },
                new[] { 0.0, 1.0, 0.0, 1.0 },
                new[] { new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0) });
            Assert.Equal(25, slot.ToMesh(MeshOptions.Strips(0.0)).FaceCount);
            Assert.Equal(1, slot.ToMesh(MeshKind.Convex).FaceCount);
        }

        [Fact]
        public void TheMesh()
        {
            GeoMesh3 panels = Panels();

            GeoPolygon3 first = panels.GetFace(0);
            int[] corners = panels.GetFaceIndices(0);
            GeoTriangle3[] triangles = panels.ToTriangles();
            GeoMesh2 elevation = panels.ToMesh2();
            string obj = new ObjWriter().Add(panels, "panels").ToString();

            Assert.Equal(corners.Length, first.VertexCount);
            Assert.Equal(panels.Area, triangles.Sum(t => t.Area), 3);
            Assert.Equal(panels.FaceCount, elevation.FaceCount);
            Assert.Contains("o panels", obj);
        }

        [Fact]
        public void BodiesInCells_PourBaysOfASlabWithAShaft()
        {
            GeoSolid3 slab = Prism(Rect(0, 0, 12000, 8000), 3000, 3250);
            GeoSolid3 shaft = Prism(Rect(5000, 3000, 6500, 4200), 2900, 3400);

            GeoSolid3 pierced = slab.WithOpenings(new[] { shaft });
            GeoCellGrid3 bays = pierced.ToCells(CellOptions3.Grid(6000, 4000, 0));

            int count = bays.CellCount;
            double volume = bays.Volume;
            GeoCell3 first = bays.Cells[0];
            GeoSolid3 pour = first.Solid;
            int[] beside = bays.GetAdjacentCells(0);

            Assert.Equal(4, count);
            Assert.Equal(23550000000.0, volume, 0);
            Assert.Equal((0, 0, 0), (first.I, first.J, first.K));
            Assert.True(pour.IsClosed());
            Assert.Equal(new[] { 1, 2 }, beside);
            Assert.DoesNotContain(bays.Cells, c => c.IsWhole);
        }

        [Fact]
        public void DividingTheAxes()
        {
            var box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 600, 400));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300));    // 16 cells, 6 whole
            Assert.Equal(16, grid.CellCount);
            Assert.Equal(6, grid.Cells.Count(c => c.IsWhole));

            GeoSolid3 cylinder = GeoSolid3.Cylinder(P(0, 0, 0), P(0, 0, 2000), 600, 48);
            GeoCellGrid3 cylinderCells = cylinder.ToCells(CellOptions3.Grid(300, 300, 500));
            Assert.Equal(64, cylinderCells.CellCount);
            Assert.Equal(16, cylinderCells.Cells.Count(c => c.IsWhole));

            GeoSolid3 column = Prism(Rect(0, 0, 400, 400), 0, 3500);
            GeoCellGrid3 lifts = column.ToCells(CellOptions3.Layers(1000));   // 1000, 1000, 1000 and 500 high
            Assert.Equal(new[] { 1000.0, 1000, 1000, 500 }, lifts.Cells.Select(c => Math.Round(c.Volume / (400.0 * 400), 6)).ToArray());

            var wallOfBlocks = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(4000, 190, 2400));
            GeoCellGrid3 blocks = wallOfBlocks.ToCells(new CellOptions3(CellAxis.BySize(390), CellAxis.Whole, CellAxis.BySize(190), 10.0));
            Assert.Equal(120, blocks.CellCount);
            Assert.All(blocks.Cells, c => Assert.True(c.IsWhole));
        }

        [Fact]
        public void WhichWayTheCellsRun_AWallTurnedInPlan()
        {
            GeoSolid3 wall = Prism(Rect(0, 0, 6000, 200), 0, 3000).TransformBy(GeoTransform3.RotationZ(Math.PI / 6));

            GeoCellGrid3 upright = wall.ToCells(CellOptions3.Grid(1000, 0, 1000), MeshPlacement3.Upright);   // 18 cells, all whole
            GeoCellGrid3 world = wall.ToCells(CellOptions3.Grid(1000, 0, 1000));                             // 18 cells, none whole

            Assert.Equal(18, upright.CellCount);
            Assert.All(upright.Cells, c => Assert.True(c.IsWhole));
            Assert.Equal(18, world.CellCount);
            Assert.DoesNotContain(world.Cells, c => c.IsWhole);
        }

        [Fact]
        public void PiecesAndSnapping()
        {
            var u = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(3000, 0), new GeoPoint2(3000, 2000), new GeoPoint2(2000, 2000), new GeoPoint2(2000, 800), new GeoPoint2(1000, 800), new GeoPoint2(1000, 2000), new GeoPoint2(0, 2000));
            GeoCellGrid3 rows = Prism(u, 0, 500).ToCells(CellOptions3.Grid(0, 1000, 0));
            Assert.Equal(2, rows.GetCellsAt(0, 1, 0).Length);

            var l = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(2000, 0), new GeoPoint2(2000, 1000), new GeoPoint2(1030, 1000), new GeoPoint2(1030, 2000), new GeoPoint2(0, 2000));
            GeoSolid3 footing = Prism(l, 0, 600);

            var options = new CellOptions3(CellAxis.BySize(500), CellAxis.BySize(500), CellAxis.Whole, snapDistance: 50);
            GeoCellGrid3 cells = footing.ToCells(options);   // the step 30 past a line of cells goes with the cell beside

            Assert.Equal(12, cells.CellCount);
            Assert.Equal(14, footing.ToCells(CellOptions3.Grid(500, 500, 0)).CellCount);
        }

        [Fact]
        public void TheCells()
        {
            var box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 600, 400));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300));

            GeoCell3 cell = grid.GetCellsAt(3, 1, 1)[0];
            GeoObb3 whole = cell.Box;
            bool full = cell.IsWhole;
            double held = cell.Volume;
            GeoObb3 empty = grid.GetBox(0, 0, 0);
            string text = new ObjWriter().Add(grid, "cell").ToString();

            Assert.Equal(300.0, whole.SizeX, 9);
            Assert.False(full);
            Assert.Equal(3000000.0, held, 3);
            Assert.Equal(300.0, empty.SizeZ, 9);
            Assert.Contains("o cell_3_1_1", text);
        }
    }
}
