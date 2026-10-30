using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A section of a body and the cells it is cut into, asked at or a little above the rounding of its coordinates, below
    /// what rounding can move a corner off a plane, a trillionth of their size, and below a millionth, are cut within that
    /// trillionth, as they are below the rounding. Cut at the tolerance asked, the rims a plane left on a plate turned about
    /// in space did not close: the section was empty, and the cells kept pieces of the plate whole across the cuts.
    /// </summary>
    public class RoundingLevelG1u2Tests
    {
        private static readonly double[] Plate = { 0, 0, 200, 0, 200, 100, 0, 100 };

        /// <summary>The plate's up, leaning in space.</summary>
        private static readonly GeoVector3 Up = Unit(new GeoVector3(0.3, -0.4, 0.866));

        /// <summary>The plate's length, square to its up.</summary>
        private static readonly GeoVector3 Along = Unit(new GeoVector3(1, 0.2, 0).Subtract(Up.Multiply(new GeoVector3(1, 0.2, 0).DotProduct(Up))));

        /// <summary>The plate's width.</summary>
        private static readonly GeoVector3 Across = Up.CrossProduct(Along);

        private static GeoVector3 Unit(GeoVector3 vector) => vector.Multiply(1 / vector.Length);

        private static Tolerance All(double value) => new Tolerance(value, value, value, value);

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
        /// The plate cut into cells 60 by 40 along its length and width from 10 before its corner, its whole height in each:
        /// widths 50, 60, 60 and 30 along it, 30, 40 and 30 across it.
        /// </summary>
        private static GeoCellGrid3 TurnedPlateCells(GeoSolid3 plate, double far, Tolerance tolerance)
        {
            MeshPlacement3 placement = MeshPlacement3.Frame(new GeoCoordinateSystem3(At(far, 0, 0, 0), Along, Across)).At(At(far, -10, -10, -2));

            return Mesh3.ToCells(plate, CellOptions3.Grid(60, 40, 10), placement, tolerance);
        }

        /// <summary>
        /// A number of roundings of a body's coordinates, a quadrillionth of the largest of them, as a tolerance.
        /// </summary>
        private static Tolerance Roundings(GeoSolid3 body, double roundings)
        {
            GeoAabb3 box = body.GetAabb();
            double largest = new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }.Max(System.Math.Abs);
            return All(roundings * 1E-15 * largest);
        }

        [Theory]
        [InlineData(1E3, 1)]
        [InlineData(1E3, 2)]
        [InlineData(1E6, 1)]
        public void AtTheRounding_APlaneThroughATurnedPlate_CutsItsWholeSection(double far, double roundings)
        {
            // At the rounding and twice it, a thousand out, and at the rounding a million out, the section was empty.
            GeoSolid3 plate = TurnedPlate(far);
            Tolerance tolerance = Roundings(plate, roundings);

            GeoFace3[] flat = plate.Section(new GeoPlane3(At(far, 70, 30, 1.2), Up), tolerance);

            Assert.Single(flat);
            Assert.Equal(20_000, flat[0].Area, 1E-6 * 20_000);
        }

        [Theory]
        [InlineData(1E3, 1)]
        [InlineData(1E3, 2)]
        [InlineData(1E6, 2)]
        public void AtTheRounding_APlaneThroughATurnedPlateWithAHole_CutsTheHoleOutOfTheSection(double far, double roundings)
        {
            // The hole 20 by 20 drawn 7 past both faces; the section was empty.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 70, 40, 90, 60, -7, 10 });

            GeoFace3[] section = plate.Section(new GeoPlane3(At(far, 30, 30, 1.2), Up), Roundings(plate, roundings));

            Assert.Single(section);
            Assert.Single(section[0].Holes);
            Assert.Equal(19_600, section[0].Area, 1E-6 * 19_600);
        }

        [Theory]
        [InlineData(1E4)]
        [InlineData(3E6)]
        [InlineData(1E7)]
        public void AtTheRounding_ATurnedPlate_IsCutIntoTwelveClosedCellsHoldingIt(double far)
        {
            // Ten thousand out the plate came out in 3 cells, three million out in 11, ten million out in 10.
            GeoSolid3 plate = TurnedPlate(far);
            Tolerance tolerance = Roundings(plate, 1);

            GeoCellGrid3 grid = TurnedPlateCells(plate, far, tolerance);

            Assert.Equal(12, grid.CellCount);
            Assert.Equal(60_000, grid.Cells.Sum(c => c.Volume), 1E-6 * 60_000);
            Assert.All(grid.Cells, c => Assert.True(c.Solid.IsClosed(tolerance)));
        }

        [Theory]
        [InlineData(1E4)]
        [InlineData(1E7)]
        public void AtTheRounding_ATurnedPlateWithAHole_IsCutIntoCellsHoldingWhatTheHoleLeaves(double far)
        {
            // The hole 20 by 20 through the cell 60 by 40 along and across the middle: 9 cells and 11 came out.
            GeoSolid3 plate = TurnedPlate(far, new double[] { 70, 40, 90, 60, -7, 10 });

            GeoCellGrid3 grid = TurnedPlateCells(plate, far, Roundings(plate, 1));

            Assert.Equal(12, grid.CellCount);
            Assert.Equal(58_800, grid.Cells.Sum(c => c.Volume), 1E-6 * 58_800);
            Assert.Equal(6_000, grid.Cells.Single(c => c.I == 1 && c.J == 1).Volume, 1E-6 * 6_000);
        }
    }
}
