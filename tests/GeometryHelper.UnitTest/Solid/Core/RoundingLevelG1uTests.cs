using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A section of a body and the cells it is cut into, asked below the rounding of its coordinates, nought among it, are
    /// cut within that rounding, and each cell is welded to close within the tolerance asked. Cut at the tolerance asked, the
    /// rims a plane left on a plate turned about in space did not close: the section was empty, and the cells lost the
    /// plate, or kept pieces of it whole across the cuts.
    /// </summary>
    public class RoundingLevelG1uTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        private static readonly double[] Plate = { 0, 0, 200, 0, 200, 100, 0, 100 };

        /// <summary>The plate's up, leaning in space.</summary>
        private static readonly GeoVector3 Up = Unit(new GeoVector3(0.3, -0.4, 0.866));

        /// <summary>The plate's length, square to its up.</summary>
        private static readonly GeoVector3 Along = Unit(new GeoVector3(1, 0.2, 0).Subtract(Up.Multiply(new GeoVector3(1, 0.2, 0).DotProduct(Up))));

        /// <summary>The plate's width.</summary>
        private static readonly GeoVector3 Across = Up.CrossProduct(Along);

        private static GeoVector3 Unit(GeoVector3 vector) => vector.Multiply(1 / vector.Length);

        /// <summary>The point at a length, a width and a height in the turned frame standing at (out, out, out).</summary>
        private static GeoPoint3 At(double far, double x, double y, double z)
            => new GeoPoint3(far, far, far).Add(Along.Multiply(x)).Add(Across.Multiply(y)).Add(Up.Multiply(z));

        /// <summary>
        /// The plate 200 by 100 by 3 standing in the turned frame, with openings each given as its length from and to, its width
        /// from and to and its height from and to.
        /// </summary>
        private static GeoSolid3 TurnedPlate(double far, params double[][] holes)
        {
            var corners = new List<GeoPoint3>();

            for (int i = 0; i < Plate.Length; i += 2)
            {
                corners.Add(At(far, Plate[i], Plate[i + 1], 0));
            }

            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(corners.ToArray()), Up.Multiply(3), Tolerance.Default);
            GeoSolid3[] openings = holes
                .Select(h => GeoSolid3.Extrude(
                    new GeoPolygon3(At(far, h[0], h[1], h[4]), At(far, h[2], h[1], h[4]), At(far, h[2], h[3], h[4]), At(far, h[0], h[3], h[4])),
                    Up.Multiply(h[5] - h[4]), Tolerance.Default))
                .ToArray();

            return holes.Length == 0 ? body : body.WithOpenings(openings);
        }

        /// <summary>
        /// The plate cut at nought into cells 60 by 40 along its length and width from 10 before its corner, its whole height in
        /// each: widths 50, 60, 60 and 30 along it, 30, 40 and 30 across it.
        /// </summary>
        private static GeoCellGrid3 TurnedPlateCells(GeoSolid3 plate, double far)
        {
            MeshPlacement3 placement = MeshPlacement3.Frame(new GeoCoordinateSystem3(At(far, 0, 0, 0), Along, Across)).At(At(far, -10, -10, -2));

            return Mesh3.ToCells(plate, CellOptions3.Grid(60, 40, 10), placement, Nought);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void AtToleranceNought_APlaneThroughATurnedPlate_CutsItsWholeSection(double far)
        {
            // Cut at nought, the rims the planes left did not close, and both sections were empty.
            GeoSolid3 plate = TurnedPlate(far);

            GeoFace3[] flat = plate.Section(new GeoPlane3(At(far, 70, 30, 1.2), Up), Nought);
            GeoFace3[] across = plate.Section(new GeoPlane3(At(far, 70, 30, 1.2), Along), Nought);

            Assert.Single(flat);
            Assert.Equal(20_000, flat[0].Area, 1E-6 * 20_000);
            Assert.Single(across);
            Assert.Equal(300, across[0].Area, 1E-6 * 300);
        }

        [Fact]
        public void AtToleranceNought_APlaneThroughATurnedPlateWithAHole_CutsTheHoleOutOfTheSection()
        {
            // A thousand out, the hole 20 by 20 drawn 7 past both faces.
            GeoSolid3 plate = TurnedPlate(1E3, new double[] { 90, 40, 110, 60, -7, 10 });

            GeoFace3[] section = plate.Section(new GeoPlane3(At(1E3, 30, 30, 1.2), Up), Nought);

            Assert.Single(section);
            Assert.Single(section[0].Holes);
            Assert.Equal(19_600, section[0].Area, 1E-6 * 19_600);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void AtToleranceNought_ATurnedPlate_IsCutIntoTwelveClosedCellsHoldingIt(double far)
        {
            // Cut at nought, cuts that would not close kept pieces whole across them, or the plate was lost.
            GeoCellGrid3 grid = TurnedPlateCells(TurnedPlate(far), far);
            double[] alongWidths = { 50, 60, 60, 30 };
            double[] acrossWidths = { 30, 40, 30 };

            Assert.Equal(12, grid.CellCount);
            Assert.Equal(60_000, grid.Cells.Sum(c => c.Volume), 1E-6 * 60_000);
            Assert.All(grid.Cells, c => Assert.Equal(alongWidths[c.I] * acrossWidths[c.J] * 3, c.Volume, 1E-6 * 9_000));
            Assert.All(grid.Cells, c => Assert.True(c.Solid.IsClosed(Nought)));
        }

        [Fact]
        public void AtToleranceNought_ATurnedPlateWithAHole_IsCutIntoCellsHoldingWhatTheHoleLeaves()
        {
            // A million out, the hole 20 by 20 through the cell 60 by 40 along and across the middle.
            GeoCellGrid3 grid = TurnedPlateCells(TurnedPlate(1E6, new double[] { 70, 40, 90, 60, -7, 10 }), 1E6);

            Assert.Equal(12, grid.CellCount);
            Assert.Equal(58_800, grid.Cells.Sum(c => c.Volume), 1E-6 * 58_800);
            Assert.Equal(6_000, grid.Cells.Single(c => c.I == 1 && c.J == 1).Volume, 1E-6 * 6_000);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void AtToleranceNought_ABoxTurnedAgainstTheGrid_IsCutIntoClosedCellsHoldingIt(double far)
        {
            // A box 100 by 60 by 40, turned, cut by a grid along the world's axes: at nought cuts that would not close kept
            // pieces of it whole across them, in fewer cells than the grid lays through it at the default tolerance.
            var box = new GeoObb3(At(far, 0, 0, 0), 100, 60, 40, Along, Across);
            CellOptions3 options = CellOptions3.Grid(37, 41, 43);

            GeoCellGrid3 grid = Mesh3.ToCells(box, options, MeshPlacement3.World, Nought);
            GeoCellGrid3 usual = Mesh3.ToCells(box, options, MeshPlacement3.World, Tolerance.Default);

            Assert.Equal(usual.CellCount, grid.CellCount);
            Assert.Equal(240_000, grid.Cells.Sum(c => c.Volume), 1E-6 * 240_000);
            Assert.All(grid.Cells.Zip(usual.Cells, (c, u) => (c, u)), p => Assert.Equal(p.u.Volume, p.c.Volume, 1E-6 * 240_000));
            Assert.All(grid.Cells, c => Assert.True(c.Solid.IsClosed(Nought)));
        }
    }
}
