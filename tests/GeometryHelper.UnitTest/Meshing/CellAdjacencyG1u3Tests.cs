using System;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Cells cut at a fine tolerance meet their neighbours along the grid's axes only, not the cells a place over across the
    /// plane between them as well, which share no more than an edge. The rounding let the extents of two such cells overlap
    /// by a hair along the edge, and they were read as meeting over that sliver, more than the point tolerance squared.
    /// </summary>
    public class CellAdjacencyG1u3Tests
    {
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

        /// <summary>A plate 200 by 100 by 3 in the turned frame, with a hole 20 by 20 through it where asked.</summary>
        private static GeoSolid3 Plate(double far, bool holed)
        {
            GeoSolid3 body = GeoSolid3.Extrude(
                new GeoPolygon3(At(far, 0, 0, 0), At(far, 200, 0, 0), At(far, 200, 100, 0), At(far, 0, 100, 0)), Up.Multiply(3), Tolerance.Default);

            if (!holed)
            {
                return body;
            }

            GeoSolid3 hole = GeoSolid3.Extrude(
                new GeoPolygon3(At(far, 70, 40, -7), At(far, 90, 40, -7), At(far, 90, 60, -7), At(far, 70, 60, -7)), Up.Multiply(17), Tolerance.Default);

            return body.WithOpenings(new[] { hole });
        }

        [Theory]
        [InlineData(0.0, false, 0.0)]
        [InlineData(0.0, false, 1E-12)]
        [InlineData(0.0, false, 1E-9)]
        [InlineData(0.0, true, 1E-12)]
        [InlineData(0.0, true, 1E-9)]
        [InlineData(1E3, false, 1E-12)]
        [InlineData(1E3, true, 1E-9)]
        [InlineData(0.0, false, 1E-3)]
        public void ATurnedPlateCutIntoTwelveCells_MeetsOnlyTheCellsBesideEachAlongAnAxis(double far, bool holed, double tolerance)
        {
            // Cells 60 by 40 along its length and width from 10 before its corner, four along it and three across: 17 pairs
            // meet. At the origin, at nought, 25 were found, the cells a place over along both axes among them.
            MeshPlacement3 placement = MeshPlacement3.Frame(new GeoCoordinateSystem3(At(far, 0, 0, 0), Along, Across)).At(At(far, -10, -10, -2));
            GeoCellGrid3 grid = Mesh3.ToCells(Plate(far, holed), CellOptions3.Grid(60, 40, 10), placement, new Tolerance(tolerance, tolerance, tolerance, tolerance));

            Assert.Equal(12, grid.CellCount);
            int pairs = 0;

            for (int n = 0; n < grid.CellCount; n++)
            {
                foreach (int m in grid.GetAdjacentCells(n))
                {
                    GeoCell3 cell = grid.Cells[n];
                    GeoCell3 other = grid.Cells[m];
                    Assert.Equal(1, Math.Abs(cell.I - other.I) + Math.Abs(cell.J - other.J) + Math.Abs(cell.K - other.K));
                    pairs++;
                }
            }

            Assert.Equal(2 * 17, pairs);
        }
    }
}
