using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Boxes and bodies cut into cells: whole where the body fills them, cut where its boundary crosses them, in pieces where
    /// it holds a cell twice, and no slice thinner than the snap distance.
    /// </summary>
    public class CellTests : IDisposable
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private readonly List<string> _warnings = new List<string>();

        /// <summary>
        /// Every cut a test asks for is made: one the body would not take is kept whole and warned of, and fails the test.
        /// </summary>
        public CellTests()
        {
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    lock (_warnings)
                    {
                        _warnings.Add(message);
                    }
                }
            };
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            Assert.Empty(_warnings);
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 Q(double x, double y) => new GeoPoint2(x, y);

        private static GeoSolid3 Prism(GeoPolygon2 plan, double z0, double z1)
            => GeoSolid3.Extrude(new GeoPolygon3(plan.Vertices.Select(p => P(p.X, p.Y, z0)), Tolerance), new GeoVector3(0, 0, z1 - z0), Tolerance);

        private static GeoPolygon2 Rect(double x0, double y0, double x1, double y1) => new GeoPolygon2(Q(x0, y0), Q(x1, y0), Q(x1, y1), Q(x0, y1));

        private static int Whole(GeoCellGrid3 grid) => grid.Cells.Count(c => c.IsWhole);

        /// <summary>The extent of a cell's material along one of the grid's axes.</summary>
        private static double Extent(GeoCell3 cell, GeoCoordinateSystem3 frame, int axis)
        {
            double[] d = cell.Solid.Faces.SelectMany(f => f.Boundary.Vertices).Select(p => frame.ToLocal(p)).Select(l => axis == 0 ? l.X : axis == 1 ? l.Y : l.Z).ToArray();
            return d.Max() - d.Min();
        }

        private static PointLocation InBox(GeoAabb3 box, GeoPoint3 p) => box.Locate(p, Tolerance);

        #region Boxes

        [Fact]
        public void ABoxLaysWholeCellsWhereItDividesByThem()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1200, 600, 900));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);

            Assert.Equal(4, grid.CountX);
            Assert.Equal(2, grid.CountY);
            Assert.Equal(3, grid.CountZ);
            Assert.Equal(24, grid.CellCount);
            Assert.Equal(24, Whole(grid));
            Assert.Equal(P(0, 0, 0), grid.Frame.Origin);
            CellAssert.IsSound(grid, p => InBox(box, p), box.Volume, Tolerance.EqualPoint, Tolerance);

            foreach (GeoCell3 cell in grid.Cells)
            {
                Assert.Equal(27E6, cell.Volume, 3);
                Assert.Equal(P(150 + 300 * cell.I, 150 + 300 * cell.J, 150 + 300 * cell.K), cell.Box.Center);
            }
        }

        [Fact]
        public void WhatIsLeftOverIsCutOffAtTheFarSide()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1000, 600, 400));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);

            Assert.Equal(16, grid.CellCount);
            Assert.Equal(6, Whole(grid));
            CellAssert.IsSound(grid, p => InBox(box, p), box.Volume, Tolerance.EqualPoint, Tolerance);

            GeoCell3 corner = grid.GetCellsAt(3, 1, 1).Single();
            Assert.False(corner.IsWhole);
            Assert.Equal(100.0 * 300.0 * 100.0, corner.Volume, 6);
            Assert.Equal(300.0, corner.Box.SizeX, 9);
        }

        [Theory]
        [InlineData(GridAlignment.Start, 0.0)]
        [InlineData(GridAlignment.End, 100.0)]
        [InlineData(GridAlignment.CenterCell, 50.0)]
        [InlineData(GridAlignment.CenterJoint, 200.0)]
        public void TheAlignmentSaysWhereTheCutCellsGo(GridAlignment alignment, double firstWholeStart)
        {
            // 1000 long in cells of 300, as the plane lays them.
            var bar = new GeoAabb3(P(0, 0, 0), P(1000, 100, 100));
            GeoCellGrid3 grid = bar.ToCells(new CellOptions3(CellAxis.BySize(300, alignment), CellAxis.Whole, CellAxis.Whole), Tolerance);

            GeoCell3 first = grid.Cells.Where(c => c.IsWhole).OrderBy(c => c.I).First();
            Assert.Equal(firstWholeStart, first.Box.Center.X - 150.0, 9);
            CellAssert.IsSound(grid, p => InBox(bar, p), bar.Volume, Tolerance.EqualPoint, Tolerance);

            if (alignment == GridAlignment.CenterCell || alignment == GridAlignment.CenterJoint)
            {
                Assert.Equal(grid.Cells.First().Volume, grid.Cells.Last().Volume, 6);
            }
        }

        [Fact]
        public void TheAlignmentPlacesTheJointsToo()
        {
            // 1000 long in cells of 300 a joint of 20 apart.
            var bar = new GeoAabb3(P(0, 0, 0), P(1000, 100, 100));
            double[] Starts(GridAlignment alignment)
            {
                GeoCellGrid3 grid = bar.ToCells(new CellOptions3(CellAxis.BySize(300, alignment), CellAxis.Whole, CellAxis.Whole, 20), Tolerance);
                return Enumerable.Range(0, grid.CountX).Select(i => Math.Round(grid.GetBox(i, 0, 0).Center.X - 150.0, 9)).ToArray();
            }

            // A cell ends at the far side; the joint between two cells stands on the middle, 490 to 510.
            Assert.Equal(new[] { -260.0, 60, 380, 700 }, Starts(GridAlignment.End));
            Assert.Equal(new[] { -130.0, 190, 510, 830 }, Starts(GridAlignment.CenterJoint));
            Assert.Equal(new[] { 0.0, 320, 640, 960 }, Starts(GridAlignment.Start));
            Assert.Equal(new[] { -290.0, 30, 350, 670, 990 }, Starts(GridAlignment.CenterCell));
        }

        [Fact]
        public void EqualCellsByCountEndExactlyAtTheFarSide()
        {
            CellGrid3Lines lines = CellGrid3Lines.Of(CellAxis.ByCount(3), 0.1, 1000.3, 0.0, Tolerance);

            Assert.Equal(1000.3, lines.Ends[2]);
            Assert.Equal(lines.Starts[1], lines.Ends[0]);
            Assert.Equal(lines.Starts[2], lines.Ends[1]);

            CellGrid3Lines jointed = CellGrid3Lines.Of(CellAxis.ByCount(7), 0.1, 1000.3, 3.0, Tolerance);
            Assert.Equal(1000.3, jointed.Ends[6]);
        }

        [Fact]
        public void AnOriginPutsACellsCornerOnIt()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1000, 1000, 1000));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 400, 1000), MeshPlacement3.World.At(P(50, -10000, 7)), Tolerance);

            // Along X the cells start at 50 + 300k, along Y at 400k, along Z at 7 + 1000k.
            Assert.Equal(new[] { -250.0, 50, 350, 650, 950 }, Enumerable.Range(0, grid.CountX).Select(i => Math.Round(grid.GetBox(i, 0, 0).Center.X - 150.0, 9)).ToArray());
            Assert.Equal(new[] { 0.0, 400, 800 }, Enumerable.Range(0, grid.CountY).Select(j => Math.Round(grid.GetBox(0, j, 0).Center.Y - 200.0, 9)).ToArray());
            Assert.Equal(new[] { -993.0, 7 }, Enumerable.Range(0, grid.CountZ).Select(k => Math.Round(grid.GetBox(0, 0, k).Center.Z - 500.0, 9)).ToArray());
            CellAssert.IsSound(grid, p => InBox(box, p), box.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void JointsLeaveGapsBetweenTheCellsAndNoneAtTheBoundary()
        {
            // Blocks of 390 by 190 with joints of 10: 10 along a wall 4000 long, 12 up 2400.
            var wall = new GeoAabb3(P(0, 0, 0), P(4000, 190, 2400));
            GeoCellGrid3 blocks = wall.ToCells(new CellOptions3(CellAxis.BySize(390), CellAxis.Whole, CellAxis.BySize(190), 10.0), Tolerance);

            Assert.Equal(120, blocks.CellCount);
            Assert.Equal(120, Whole(blocks));
            CellAssert.IsSound(blocks, p => InBox(wall, p), 120 * 390.0 * 190 * 190, Tolerance.EqualPoint, Tolerance, covers: false);

            for (int n = 0; n < blocks.CellCount; n++)
            {
                Assert.Empty(blocks.GetAdjacentCells(n));
            }
        }

        [Fact]
        public void ACountDividesTheBodyIntoEqualCells()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1000, 600, 400));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Divide(3, 2, 1), Tolerance);

            Assert.Equal(6, grid.CellCount);
            Assert.Equal(6, Whole(grid));
            Assert.All(grid.Cells, c => Assert.Equal(1000.0 / 3 * 300 * 400, c.Volume, 3));
            CellAssert.IsSound(grid, p => InBox(box, p), box.Volume, Tolerance.EqualPoint, Tolerance);

            GeoCellGrid3 jointed = box.ToCells(CellOptions3.Divide(4, 1, 1, joint: 20), Tolerance);
            Assert.Equal(4, jointed.CellCount);
            Assert.Equal((1000.0 - 3 * 20) * 600 * 400, jointed.Volume, 3);
        }

        [Fact]
        public void LayersAreLiftsUpZ()
        {
            var column = new GeoAabb3(P(0, 0, 0), P(400, 400, 3500));
            GeoCellGrid3 lifts = column.ToCells(CellOptions3.Layers(1000), Tolerance);

            Assert.Equal(new[] { 1, 1, 4 }, new[] { lifts.CountX, lifts.CountY, lifts.CountZ });
            Assert.Equal(new[] { 1000.0, 1000, 1000, 500 }, lifts.Cells.Select(c => Math.Round(c.Volume / (400.0 * 400), 6)).ToArray());
            CellAssert.IsSound(lifts, p => InBox(column, p), column.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void ACutWithinTheSnapDistanceOfASideGoesOntoIt()
        {
            // A box a hair over two cells long: the hair is no cell of its own, but part of the last.
            var box = new GeoAabb3(P(0, 0, 0), P(1000.005, 500, 500));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(500, 500, 500), Tolerance);

            Assert.Equal(2, grid.CellCount);
            Assert.Equal(2, Whole(grid));
            Assert.Equal(500.005 * 500 * 500, grid.Cells[1].Volume, 3);

            // A snap distance of 50 takes a slice of 30 into the cell beside, which is no longer whole.
            var longer = new GeoAabb3(P(0, 0, 0), P(1030, 500, 500));
            GeoCellGrid3 snapped = longer.ToCells(new CellOptions3(CellAxis.BySize(500), CellAxis.Whole, CellAxis.Whole, snapDistance: 50), Tolerance);

            Assert.Equal(2, snapped.CellCount);
            Assert.Equal(530.0 * 500 * 500, snapped.Cells[1].Volume, 3);
            Assert.False(snapped.Cells[1].IsWhole);
            CellAssert.IsSound(snapped, p => InBox(longer, p), longer.Volume, 50, Tolerance);
        }

        [Theory]
        [InlineData(5.0, 10.0)]
        [InlineData(0.03, 0.0)]
        [InlineData(30.0, 40.0)]
        public void ABoxThinnerThanTheSnapDistanceKeepsItsCells(double thickness, double snap)
        {
            // Along its thickness the box is no thicker than the snap distance, or four point tolerances: every cell's far
            // side is within it of the box's near side, and went onto it.
            var plate = new GeoAabb3(P(0, 0, 0), P(1000, 1000, thickness));
            GeoCellGrid3 grid = plate.ToCells(new CellOptions3(CellAxis.BySize(100), CellAxis.BySize(100), CellAxis.Whole, 0, snap), Tolerance);

            Assert.Equal(100, grid.CellCount);
            Assert.Equal(plate.Volume, grid.Volume, 6);

            var turned = new GeoObb3(P(0, 0, 0), 2000, 1000, thickness, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));
            GeoCellGrid3 own = turned.ToCells(new CellOptions3(CellAxis.BySize(500), CellAxis.BySize(500), CellAxis.Whole, 0, snap), Tolerance);

            Assert.Equal(8, own.CellCount);
            Assert.Equal(turned.Volume, own.Volume, 6);
        }

        [Fact]
        public void ABoxAndTheSameBodyGiveAThinPlateToOneLayer()
        {
            // Layers of 2 through a plate 5 thick with a snap distance of 10: each cut comes within it of a side of the plate,
            // and the plate goes whole to one layer, the same one as a box and as a body.
            var plate = new GeoAabb3(P(0, 0, 0), P(1000, 100, 5));
            var options = new CellOptions3(CellAxis.Whole, CellAxis.Whole, CellAxis.BySize(2), 0, 10);
            GeoCellGrid3 box = plate.ToCells(options, Tolerance);
            GeoCellGrid3 body = Prism(Rect(0, 0, 1000, 100), 0, 5).ToCells(options, Tolerance);

            GeoCell3 inBox = Assert.Single(box.Cells);
            GeoCell3 inBody = Assert.Single(body.Cells);
            Assert.Equal(inBox.K, inBody.K);
            Assert.Equal(plate.Volume, inBox.Volume, 6);
            Assert.Equal(plate.Volume, inBody.Volume, 3);
            CellAssert.IsSound(body, p => InBox(plate, p), plate.Volume, 10, Tolerance, samples: 6000);
        }

        [Fact]
        public void TooManyCellsOrCellsTooSmallAreRefused()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(10000, 10000, 10000));

            Assert.Throws<ArgumentException>(() => box.ToCells(CellOptions3.Grid(10, 10, 10), Tolerance));
            Assert.Throws<ArgumentException>(() => box.ToCells(CellOptions3.Grid(0.005, 0, 0), Tolerance));
            Assert.Throws<ArgumentException>(() => new GeoAabb3(P(0, 0, 0), P(1, 1, 1)).ToCells(CellOptions3.Divide(1000, 1, 1), Tolerance));
        }

        [Fact]
        public void AnEmptyOrFlatBoxHasNoCells()
        {
            Assert.Equal(0, GeoAabb3.Empty.ToCells(CellOptions3.Grid(100, 100, 100), Tolerance).CellCount);
            Assert.Equal(0, new GeoObb3(P(0, 0, 0), 1000, 1000, 0).ToCells(CellOptions3.Grid(100, 100, 100), Tolerance).CellCount);
        }

        [Fact]
        public void ATurnedBoxIsCutAlongItsOwnSidesUnlessToldOtherwise()
        {
            var box = new GeoObb3(P(500, 500, 500), 2000, 1000, 600, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0.2));
            GeoCellGrid3 own = box.ToCells(CellOptions3.Grid(500, 500, 300), Tolerance);
            GeoCellGrid3 world = box.ToCells(CellOptions3.Grid(500, 500, 300), MeshPlacement3.World, Tolerance);

            Assert.Equal(16, own.CellCount);
            Assert.Equal(16, Whole(own));
            Assert.Equal(1.0, own.Frame.XAxis.DotProduct(box.AxisX), 12);
            Assert.True(world.CellCount > own.CellCount);
            Assert.Equal(1.0, world.Frame.XAxis.X, 12);
            CellAssert.IsSound(own, p => box.Locate(p, Tolerance), box.Volume, Tolerance.EqualPoint, Tolerance);
            CellAssert.IsSound(world, p => box.Locate(p, Tolerance), box.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void NeighboursShareAFace()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(900, 900, 900));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);

            int middle = grid.Cells.ToList().FindIndex(c => c.I == 1 && c.J == 1 && c.K == 1);
            int corner = grid.Cells.ToList().FindIndex(c => c.I == 0 && c.J == 0 && c.K == 0);

            Assert.Equal(6, grid.GetAdjacentCells(middle).Length);
            Assert.Equal(3, grid.GetAdjacentCells(corner).Length);

            foreach (int n in grid.GetAdjacentCells(middle))
            {
                GeoCell3 other = grid.Cells[n];
                Assert.Equal(1, Math.Abs(other.I - 1) + Math.Abs(other.J - 1) + Math.Abs(other.K - 1));
            }
        }

        [Fact]
        public void AnObjFileGivesEveryCellAsAnObjectOfItsOwn()
        {
            var u = new GeoPolygon2(Q(0, 0), Q(3000, 0), Q(3000, 2000), Q(2000, 2000), Q(2000, 800), Q(1000, 800), Q(1000, 2000), Q(0, 2000));
            GeoCellGrid3 grid = Prism(u, 0, 500).ToCells(CellOptions3.Grid(0, 1000, 0), Tolerance);
            string[] lines = new GeometryHelper.Export.ObjWriter().Add(grid, "pour").ToString().Split('\n');

            Assert.Equal(new[] { "o pour_0_0_0", "o pour_0_1_0", "o pour_0_1_0_1" }, lines.Where(l => l.StartsWith("o ", StringComparison.Ordinal)).ToArray());
            Assert.Equal(grid.Cells.Sum(c => c.Solid.Triangulate().Length), lines.Count(l => l.StartsWith("f ", StringComparison.Ordinal)));
        }

        [Fact]
        public void CellsAreFoundByTheirIndexes()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1000, 600, 400));
            GeoCellGrid3 grid = box.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);

            Assert.Single(grid.GetCellsAt(2, 1, 0));
            Assert.Empty(grid.GetCellsAt(9, 9, 9));
            Assert.Equal(P(1050, 450, 450), grid.GetBox(3, 1, 1).Center);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetBox(4, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetAdjacentCells(16));
            Assert.Equal(16, grid.GetSolids().Length);
            Assert.Contains("4 x 2 x 2", grid.ToString());
            Assert.Contains("Cell3(3, 1, 1, cut", grid.GetCellsAt(3, 1, 1)[0].ToString());
        }

        #endregion

        #region Bodies

        [Fact]
        public void ABodyThatIsABoxIsCutAsTheBoxIs()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(1000, 600, 400));
            GeoSolid3 solid = box.ToObb().ToSolid();

            GeoCellGrid3 byBox = box.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);
            GeoCellGrid3 byBody = solid.ToCells(CellOptions3.Grid(300, 300, 300), Tolerance);

            Assert.Equal(byBox.CellCount, byBody.CellCount);
            Assert.Equal(Whole(byBox), Whole(byBody));

            for (int n = 0; n < byBox.CellCount; n++)
            {
                Assert.Equal((byBox.Cells[n].I, byBox.Cells[n].J, byBox.Cells[n].K), (byBody.Cells[n].I, byBody.Cells[n].J, byBody.Cells[n].K));
                Assert.Equal(byBox.Cells[n].Volume, byBody.Cells[n].Volume, 3);
            }

            CellAssert.IsSound(byBody, p => solid.Locate(p, Tolerance), solid.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void AStepAHairOffAGridLineLeavesNoSliver()
        {
            // An L-shaped footing whose step stands five thousandths past the line between two cells.
            var l = new GeoPolygon2(Q(0, 0), Q(2000, 0), Q(2000, 1000), Q(1000.005, 1000), Q(1000.005, 2000), Q(0, 2000));
            GeoSolid3 footing = Prism(l, 0, 800);
            GeoCellGrid3 grid = footing.ToCells(CellOptions3.Grid(500, 500, 400), Tolerance);

            Assert.Equal(24, grid.CellCount);
            Assert.Equal(24, Whole(grid));
            Assert.All(grid.Cells, c => Assert.True(Extent(c, grid.Frame, 0) > 499.99, $"{c} is {Extent(c, grid.Frame, 0)} along X"));
            CellAssert.IsSound(grid, p => footing.Locate(p, Tolerance), footing.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void ATipAHairPastACutStaysWithTheRest()
        {
            // A pyramid whose apex stands fourteen thousandths past the second line of lifts, a little more than the point
            // tolerance a cut is snapped within: cut off, the tip would be a piece the size of the tolerance.
            var pyramid = new GeoSolid3(
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 1000, 0), P(1000, 1000, 0), P(1000, 0, 0))),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(1000, 0, 0), P(500, 500, 1000.014))),
                new GeoFace3(new GeoPolygon3(P(1000, 0, 0), P(1000, 1000, 0), P(500, 500, 1000.014))),
                new GeoFace3(new GeoPolygon3(P(1000, 1000, 0), P(0, 1000, 0), P(500, 500, 1000.014))),
                new GeoFace3(new GeoPolygon3(P(0, 1000, 0), P(0, 0, 0), P(500, 500, 1000.014))));
            GeoCellGrid3 lifts = pyramid.ToCells(CellOptions3.Layers(500), Tolerance);

            Assert.Equal(2, lifts.CellCount);
            Assert.Equal(pyramid.Volume, lifts.Volume, 3);
            CellAssert.IsSound(lifts, p => pyramid.Locate(p, Tolerance), pyramid.Volume, 4 * Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void TheTipOfANeedleThinnerThanTheToleranceStaysWithTheRest()
        {
            // A needle 800 long on a base 25 across, its point a quarter of a millimetre past the line between two cells:
            // across the line it is eight thousandths wide, less than the point tolerance, and no piece can be cut there.
            GeoPoint3 a = P(200, 0, 0), b = P(200, 25, 0), c = P(200, 0, 25), d = P(1000.244, 5, 5);
            var needle = new GeoSolid3(
                new GeoFace3(new GeoPolygon3(a, c, b)),
                new GeoFace3(new GeoPolygon3(a, b, d)),
                new GeoFace3(new GeoPolygon3(b, c, d)),
                new GeoFace3(new GeoPolygon3(c, a, d)));
            GeoCellGrid3 grid = needle.ToCells(CellOptions3.Grid(800, 0, 0), Tolerance);

            Assert.True(needle.IsClosed(Tolerance));
            Assert.True(needle.GetSignedVolume() > 0);
            Assert.Equal(1, grid.CellCount);
            Assert.Equal(needle.Volume, grid.Volume, 6);
        }

        [Fact]
        public void AFlangeThinnerThanTheSnapDistanceStaysInTheCellsItCrosses()
        {
            // An L standing in XZ, a flange 1000 long and 5 thick and a web 100 wide and 500 tall: past the web the flange is
            // thinner along Z than the snap distance, and lies wholly above the cut before the first layer.
            var l = new[] { Q(0, 0), Q(1000, 0), Q(1000, 5), Q(100, 5), Q(100, 500), Q(0, 500) };
            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(l.Select(p => P(p.X, 0, p.Y)), Tolerance), new GeoVector3(0, 100, 0), Tolerance);
            GeoCellGrid3 grid = body.ToCells(new CellOptions3(CellAxis.BySize(200), CellAxis.Whole, CellAxis.BySize(100), 0, 6), Tolerance);

            Assert.Equal(5.45E6, body.Volume, 3);
            CellAssert.IsSound(grid, p => body.Locate(p, Tolerance), body.Volume, 6, Tolerance);
        }

        [Fact]
        public void ALedgeAboveALineOfLayersGoesToTheLayerItStandsIn()
        {
            // A column with a ledge 3 thick sticking out at 205: the ledge stands wholly above the line at 200, within the snap
            // distance of it, and belongs to the layer above.
            GeoSolid3 column = Prism(Rect(0, 0, 100, 100), 0, 300);
            Assert.True(column.TryUnion(Prism(Rect(100, 0, 200, 100), 205, 208), out GeoSolid3 body, Tolerance));
            GeoCellGrid3 grid = body.ToCells(new CellOptions3(CellAxis.BySize(100), CellAxis.Whole, CellAxis.BySize(100), 0, 10), Tolerance);

            GeoCell3 ledge = Assert.Single(grid.Cells, c => c.I == 1);
            Assert.Equal(2, ledge.K);
            Assert.Equal(100.0 * 100 * 3, ledge.Volume, 3);
            CellAssert.IsSound(grid, p => body.Locate(p, Tolerance), body.Volume, 10, Tolerance);
        }

        [Fact]
        public void ACutMovedOntoACornerTakesNoTipOffTheFarSide()
        {
            // The end of the body slopes from 100.008 to 100.042: the cut at 100 is moved onto the corner at 100.008, and from
            // there would take off a slice 0.034 thick, less than four point tolerances.
            GeoSolid3 body = Prism(new GeoPolygon2(Q(0, 0), Q(100.008, 0), Q(100.042, 100), Q(0, 100)), 0, 100);
            GeoCellGrid3 grid = body.ToCells(CellOptions3.Grid(100, 0, 0), Tolerance);

            GeoCell3 cell = Assert.Single(grid.Cells);
            Assert.Equal(body.Volume, cell.Volume, 3);

            // So too with a snap distance of 60 and the slope from 159.99 to 160.02.
            GeoSolid3 longer = Prism(new GeoPolygon2(Q(0, 0), Q(159.99, 0), Q(160.02, 100), Q(0, 100)), 0, 100);
            GeoCellGrid3 snapped = longer.ToCells(new CellOptions3(CellAxis.BySize(100), CellAxis.Whole, CellAxis.Whole, 0, 60), Tolerance);

            Assert.All(snapped.Cells, c => Assert.True(Extent(c, snapped.Frame, 0) > 4 * Tolerance.EqualPoint, $"{c} is {Extent(c, snapped.Frame, 0)} along X"));
            CellAssert.IsSound(snapped, p => longer.Locate(p, Tolerance), longer.Volume, 60, Tolerance);
        }

        [Fact]
        public void ALargerSnapDistanceTakesThickerSlicesToTheCellBeside()
        {
            // The step 30 past the line: with the point tolerance it leaves a slice 30 thick, with 50 it does not.
            var l = new GeoPolygon2(Q(0, 0), Q(2000, 0), Q(2000, 1000), Q(1030, 1000), Q(1030, 2000), Q(0, 2000));
            GeoSolid3 footing = Prism(l, 0, 800);

            GeoCellGrid3 tight = footing.ToCells(CellOptions3.Grid(500, 500, 0), Tolerance);
            GeoCellGrid3 snapped = footing.ToCells(new CellOptions3(CellAxis.BySize(500), CellAxis.BySize(500), CellAxis.Whole, snapDistance: 50), Tolerance);

            Assert.Contains(tight.Cells, c => Extent(c, tight.Frame, 0) < 31);
            Assert.All(snapped.Cells, c => Assert.True(Extent(c, snapped.Frame, 0) > 450, $"{c} is {Extent(c, snapped.Frame, 0)} along X"));
            CellAssert.IsSound(tight, p => footing.Locate(p, Tolerance), footing.Volume, Tolerance.EqualPoint, Tolerance);
            CellAssert.IsSound(snapped, p => footing.Locate(p, Tolerance), footing.Volume, 50, Tolerance);
        }

        [Fact]
        public void ACellAcrossTheNotchOfAUComesInTwoPieces()
        {
            var u = new GeoPolygon2(Q(0, 0), Q(3000, 0), Q(3000, 2000), Q(2000, 2000), Q(2000, 800), Q(1000, 800), Q(1000, 2000), Q(0, 2000));
            GeoSolid3 body = Prism(u, 0, 500);
            GeoCellGrid3 grid = body.ToCells(CellOptions3.Grid(0, 1000, 0), Tolerance);

            Assert.Equal(3, grid.CellCount);
            GeoCell3[] arms = grid.GetCellsAt(0, 1, 0);
            Assert.Equal(2, arms.Length);
            Assert.Equal(new[] { 0, 1 }, arms.Select(c => c.Piece).ToArray());
            Assert.True(grid.Frame.ToLocal(arms[0].Solid.Centroid).X < grid.Frame.ToLocal(arms[1].Solid.Centroid).X, "the pieces come in turn along X");
            Assert.All(arms, a => Assert.Equal(1000.0 * 1000 * 500, a.Volume, 3));

            // The base meets both arms, the arms do not meet each other.
            Assert.Equal(new[] { 1, 2 }, grid.GetAdjacentCells(0));
            Assert.Equal(new[] { 0 }, grid.GetAdjacentCells(1));
            Assert.Equal(new[] { 0 }, grid.GetAdjacentCells(2));
            CellAssert.IsSound(grid, p => body.Locate(p, Tolerance), body.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void ACutBetweenTwoArmsOfAPieceSeparatesThem()
        {
            // A U with its arms along X: the column of cells over the arms holds both, apart, and the cut along Y between them
            // touches neither.
            var u = new GeoPolygon2(Q(0, 0), Q(2000, 0), Q(2000, 1000), Q(800, 1000), Q(800, 2000), Q(2000, 2000), Q(2000, 3000), Q(0, 3000));
            GeoSolid3 body = Prism(u, 0, 500);
            GeoCellGrid3 grid = body.ToCells(CellOptions3.Grid(1000, 1500, 0), Tolerance);

            Assert.Equal(4, grid.CellCount);
            Assert.Single(grid.GetCellsAt(1, 0, 0));
            Assert.Single(grid.GetCellsAt(1, 1, 0));
            Assert.Equal(1000.0 * 1000 * 500, grid.GetCellsAt(1, 0, 0)[0].Volume, 3);
            Assert.Equal(1000.0 * 1000 * 500, grid.GetCellsAt(1, 1, 0)[0].Volume, 3);
            CellAssert.IsSound(grid, p => body.Locate(p, Tolerance), body.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void OpeningsAreCutInFirst()
        {
            GeoSolid3 slab = Prism(Rect(0, 0, 12000, 8000), 3000, 3250);
            GeoSolid3 shaft = Prism(Rect(5000, 3000, 6500, 4200), 2900, 3400);
            GeoSolid3 pierced = slab.WithOpenings(new[] { shaft });
            GeoCellGrid3 bays = pierced.ToCells(CellOptions3.Grid(6000, 4000, 0), Tolerance);

            Assert.Equal(4, bays.CellCount);
            Assert.Equal(0, Whole(bays));
            Assert.Equal(pierced.GetNetVolume(Tolerance), bays.Volume, 0);
            CellAssert.IsSound(bays, p => pierced.Locate(p, Tolerance), pierced.GetNetVolume(Tolerance), Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void ABodyOpeningsTakeWhollyHasNoCells()
        {
            GeoSolid3 block = Prism(Rect(0, 0, 1000, 1000), 0, 1000);
            GeoSolid3 bigger = Prism(Rect(-10, -10, 1010, 1010), -10, 1010);

            GeoCellGrid3 grid = block.WithOpenings(new[] { bigger }).ToCells(CellOptions3.Grid(500, 500, 500), Tolerance);
            Assert.Equal(0, grid.CellCount);
            Assert.Equal(8, grid.CountX * grid.CountY * grid.CountZ);
        }

        [Fact]
        public void ABodyThatIsNotClosedIsRefused()
        {
            GeoSolid3 box = new GeoAabb3(P(0, 0, 0), P(1000, 1000, 1000)).ToObb().ToSolid();
            var open = new GeoSolid3(box.Faces.Skip(1));

            Assert.Throws<ArgumentException>(() => open.ToCells(CellOptions3.Grid(500, 500, 500), Tolerance));
        }

        [Fact]
        public void ACylinderKeepsItsVolumeCellByCell()
        {
            GeoSolid3 cylinder = GeoSolid3.Cylinder(P(0, 0, 0), P(0, 0, 2000), 600, 48, Tolerance);
            GeoCellGrid3 grid = cylinder.ToCells(CellOptions3.Grid(300, 300, 500), Tolerance);

            Assert.Equal(64, grid.CellCount);
            Assert.Equal(16, Whole(grid));
            CellAssert.IsSound(grid, p => cylinder.Locate(p, Tolerance), cylinder.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void UprightRunsAWallsCellsAlongIt()
        {
            // A wall 6000 long, 200 thick and 3000 high, turned 30 degrees in plan.
            GeoSolid3 wall = Prism(Rect(0, 0, 6000, 200), 0, 3000).TransformBy(GeoTransform3.RotationZ(Math.PI / 6));
            GeoCellGrid3 upright = wall.ToCells(CellOptions3.Grid(1000, 0, 1000), MeshPlacement3.Upright, Tolerance);
            GeoCellGrid3 world = wall.ToCells(CellOptions3.Grid(1000, 0, 1000), Tolerance);

            Assert.Equal(18, upright.CellCount);
            Assert.Equal(18, Whole(upright));
            Assert.Equal(1.0, upright.Frame.ZAxis.Z, 12);
            Assert.Equal(Math.Cos(Math.PI / 6), upright.Frame.XAxis.X, 9);
            Assert.True(Whole(world) < 18);
            CellAssert.IsSound(upright, p => wall.Locate(p, Tolerance), wall.Volume, Tolerance.EqualPoint, Tolerance);
            CellAssert.IsSound(world, p => wall.Locate(p, Tolerance), wall.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void OwnAxesStandUpAndRunAlongTheBody()
        {
            // A beam 5000 long, 300 wide and 600 deep, its length turned 20 degrees in plan.
            GeoSolid3 beam = Prism(Rect(0, 0, 5000, 300), 0, 600).TransformBy(GeoTransform3.Translation(new GeoVector3(100, 200, 3000)) * GeoTransform3.RotationZ(20 * Math.PI / 180));
            GeoCellGrid3 grid = beam.ToCells(CellOptions3.Grid(1000, 0, 0), MeshPlacement3.Own, Tolerance);

            Assert.Equal(1.0, grid.Frame.ZAxis.Z, 9);
            Assert.Equal(Math.Cos(20 * Math.PI / 180), grid.Frame.XAxis.X, 9);
            Assert.Equal(5, grid.CellCount);
            Assert.Equal(5, Whole(grid));
            CellAssert.IsSound(grid, p => beam.Locate(p, Tolerance), beam.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void ADirectionOrAFrameGivesTheAxes()
        {
            GeoSolid3 block = Prism(Rect(0, 0, 2000, 2000), 0, 1000);
            GeoCellGrid3 along = block.ToCells(CellOptions3.Grid(700, 700, 0), MeshPlacement3.Along(new GeoVector3(1, 1, 0)), Tolerance);

            Assert.Equal(Math.Sqrt(0.5), along.Frame.XAxis.X, 12);
            Assert.Equal(Math.Sqrt(0.5), along.Frame.XAxis.Y, 12);
            Assert.Equal(1.0, along.Frame.ZAxis.Z, 12);
            CellAssert.IsSound(along, p => block.Locate(p, Tolerance), block.Volume, Tolerance.EqualPoint, Tolerance);

            var frame = new GeoCoordinateSystem3(P(0, 0, 0), new GeoVector3(0, 1, 0), new GeoVector3(0, 0, 1));
            GeoCellGrid3 framed = block.ToCells(CellOptions3.Grid(700, 700, 700), MeshPlacement3.Frame(frame), Tolerance);
            Assert.Equal(frame.XAxis, framed.Frame.XAxis);
            Assert.Equal(frame.ZAxis, framed.Frame.ZAxis);
            CellAssert.IsSound(framed, p => block.Locate(p, Tolerance), block.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void AnOriginFarAwayPlacesTheCellsAsOneNearBy()
        {
            // A footing seven kilometres out, the cells starting at whole steps of 1000 from the world's origin.
            GeoSolid3 footing = Prism(Rect(7012345.5, 512345.25, 7015345.5, 514345.25), 31.5, 1031.5);
            GeoCellGrid3 grid = footing.ToCells(CellOptions3.Grid(1000, 1000, 0), MeshPlacement3.World.At(P(0, 0, 0)), Tolerance);

            Assert.Equal(0.0, grid.Frame.Origin.X % 1000.0, 6);
            Assert.Equal(0.0, grid.Frame.Origin.Y % 1000.0, 6);
            Assert.Equal(4, grid.CountX);
            Assert.Equal(3, grid.CountY);
            CellAssert.IsSound(grid, p => footing.Locate(p, Tolerance), footing.Volume, Tolerance.EqualPoint, Tolerance);
        }

        [Fact]
        public void JointsInABodyLeaveTheirGapsEmpty()
        {
            GeoSolid3 slab = Prism(Rect(0, 0, 6010, 4000), 0, 250);
            GeoCellGrid3 bays = slab.ToCells(CellOptions3.Grid(1990, 1990, 0, joint: 20), Tolerance);

            // Three bays of 1990 a joint of 20 apart along 6010, two along 4000: what the joints take is gone.
            Assert.Equal(6, bays.CellCount);
            Assert.Equal(6, Whole(bays));
            CellAssert.IsSound(bays, p => slab.Locate(p, Tolerance), 6 * 1990.0 * 1990 * 250, Tolerance.EqualPoint, Tolerance, covers: false);
            Assert.All(Enumerable.Range(0, bays.CellCount), n => Assert.Empty(bays.GetAdjacentCells(n)));
        }

        [Fact]
        public void AJointIsCutOutHoweverWideTheSnapDistance()
        {
            GeoSolid3 slab = Prism(Rect(0, 0, 6010, 4000), 0, 250);
            GeoCellGrid3 bays = slab.ToCells(new CellOptions3(CellAxis.BySize(1990), CellAxis.BySize(1990), CellAxis.Whole, 20, snapDistance: 50), Tolerance);
            GeoCellGrid3 boxes = new GeoAabb3(P(0, 0, 0), P(6010, 4000, 250)).ToCells(new CellOptions3(CellAxis.BySize(1990), CellAxis.BySize(1990), CellAxis.Whole, 20, snapDistance: 50), Tolerance);

            Assert.Equal(6 * 1990.0 * 1990 * 250, bays.Volume, 0);
            Assert.Equal(6 * 1990.0 * 1990 * 250, boxes.Volume, 0);
        }

        [Fact]
        public void OneThreadCutsAsManyDo()
        {
            GeoSolid3 cylinder = GeoSolid3.Cylinder(P(0, 0, 0), P(0, 0, 2000), 600, 32, Tolerance);
            GeoCellGrid3 many = cylinder.ToCells(CellOptions3.Grid(300, 300, 500), Tolerance);
            GeoCellGrid3 one = cylinder.ToCells(new CellOptions3(CellAxis.BySize(300), CellAxis.BySize(300), CellAxis.BySize(500), maxDegreeOfParallelism: 1), Tolerance);

            Assert.Equal(many.CellCount, one.CellCount);

            for (int n = 0; n < many.CellCount; n++)
            {
                Assert.Equal((many.Cells[n].I, many.Cells[n].J, many.Cells[n].K, many.Cells[n].Piece), (one.Cells[n].I, one.Cells[n].J, one.Cells[n].K, one.Cells[n].Piece));
                Assert.Equal(many.Cells[n].Volume, one.Cells[n].Volume);
            }
        }

        #endregion

        #region The options

        [Fact]
        public void OptionsSayHowEachAxisIsDivided()
        {
            CellOptions3 grid = CellOptions3.Grid(300, 0, 200, 5, GridAlignment.CenterCell);
            Assert.Equal(CellAxis.BySize(300, GridAlignment.CenterCell), grid.X);
            Assert.True(grid.Y.IsWhole);
            Assert.Equal(200.0, grid.Z.Size);
            Assert.Equal(5.0, grid.Joint);

            Assert.True(CellOptions3.Layers(500).X.IsWhole);
            Assert.Equal(500.0, CellOptions3.Layers(500).Z.Size);
            Assert.Equal(3, CellOptions3.Divide(3, 1, 2).X.Count);
            Assert.True(CellOptions3.Divide(3, 1, 2).Y.IsWhole);
            Assert.True(CellAxis.Whole.IsWhole);
            Assert.True(default(CellAxis) == CellAxis.Whole);
            Assert.True(CellAxis.ByCount(2) != CellAxis.ByCount(3));

            Assert.Equal(CellOptions3.Grid(300, 0, 200), CellOptions3.Grid(300, 0, 200));
            Assert.Equal(CellOptions3.Grid(300, 0, 200).GetHashCode(), CellOptions3.Grid(300, 0, 200).GetHashCode());
            Assert.NotEqual(CellOptions3.Grid(300, 0, 200), CellOptions3.Grid(300, 0, 200, 1));
            Assert.Contains("Size 300", CellOptions3.Grid(300, 0, 200).ToString());
            Assert.Contains("Count 3", CellAxis.ByCount(3).ToString());
        }

        [Fact]
        public void OptionsRefuseWhatCannotBe()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellAxis.BySize(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellAxis.BySize(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellAxis.BySize(10, (GridAlignment)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellAxis.ByCount(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellOptions3.Grid(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellOptions3.Grid(1, 1, 1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellOptions3(CellAxis.Whole, CellAxis.Whole, CellAxis.Whole, snapDistance: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellOptions3(CellAxis.Whole, CellAxis.Whole, CellAxis.Whole, maxDegreeOfParallelism: 0));
            Assert.Throws<ArgumentNullException>(() => new GeoAabb3(P(0, 0, 0), P(1, 1, 1)).ToCells(null));
            Assert.Throws<ArgumentNullException>(() => Mesh3.ToCells((GeoSolid3)null, CellOptions3.Grid(1, 1, 1)));
            Assert.Throws<ArgumentNullException>(() => Mesh3.ToCells((GeoObb3)null, CellOptions3.Grid(1, 1, 1)));
            Assert.Throws<ArgumentNullException>(() => new GeoAabb3(P(0, 0, 0), P(1, 1, 1)).ToCells(CellOptions3.Grid(1, 1, 1), (MeshPlacement3)null));
        }

        #endregion
    }

    /// <summary>
    /// The cells along one axis, as the grid lays them, for the tests that read them.
    /// </summary>
    internal sealed class CellGrid3Lines
    {
        public double[] Starts { get; private set; }

        public double[] Ends { get; private set; }

        public static CellGrid3Lines Of(CellAxis axis, double min, double max, double joint, Tolerance tolerance)
        {
            GeometryHelper.Core.CellGrid3.Lines lines = GeometryHelper.Core.CellGrid3.Lay(axis, min, max, joint, false, tolerance, "X");
            return new CellGrid3Lines { Starts = lines.Starts, Ends = lines.Ends };
        }
    }
}
