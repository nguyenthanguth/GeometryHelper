using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// The options a mesh is made by, and the rectangles a rectangle divides into.
    /// </summary>
    public class MeshOptionsTests
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        [Fact]
        public void TheFactoriesGiveTheUsualOnes()
        {
            Assert.Equal(MeshKind.Triangles, MeshOptions.Triangles.Kind);
            Assert.Equal(MeshKind.Convex, MeshOptions.Convex.Kind);
            Assert.Equal(MeshKind.Strips, MeshOptions.Strips(0.5).Kind);
            Assert.Equal(0.5, MeshOptions.Strips(0.5).AngleRad);
            Assert.Null(MeshOptions.Triangles.AngleRad);

            MeshOptions grid = MeshOptions.Grid(1200, 600, 3, GridAlignment.CenterCell, GridAlignment.End);
            Assert.Equal(MeshKind.Grid, grid.Kind);
            Assert.Equal(1200, grid.CellWidth);
            Assert.Equal(600, grid.CellHeight);
            Assert.Equal(3, grid.Joint);
            Assert.Equal(GridAlignment.CenterCell, grid.AlignU);
            Assert.Equal(GridAlignment.End, grid.AlignV);
            Assert.Null(grid.Origin);

            MeshOptions anchored = MeshOptions.Grid(500, 500, P(10, 20), 5);
            Assert.Equal(P(10, 20), anchored.Origin);
            Assert.Equal(5, anchored.Joint);
        }

        [Fact]
        public void OptionsThatCannotMakeAMeshAreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshOptions((MeshKind)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(0, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, 100, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, 100, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, 100, 0, (GridAlignment)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, 100, 0, GridAlignment.Start, (GridAlignment)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Grid(100, 100, P(double.NaN, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshOptions.Strips(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshOptions(MeshKind.Triangles, chordTolerance: -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeshOptions(MeshKind.Triangles, cellWidth: -1));

            // A kind that is not a grid reads no cell size, so none is needed.
            Assert.Equal(0, new MeshOptions(MeshKind.Strips).CellWidth);
        }

        [Fact]
        public void TwoOptionsAreEqualWhenTheyMakeTheSameMesh()
        {
            Assert.Equal(MeshOptions.Grid(100, 50, 2), MeshOptions.Grid(100, 50, 2));
            Assert.Equal(MeshOptions.Grid(100, 50, 2).GetHashCode(), MeshOptions.Grid(100, 50, 2).GetHashCode());
            Assert.NotEqual(MeshOptions.Grid(100, 50, 2), MeshOptions.Grid(100, 50, 3));
            Assert.NotEqual(MeshOptions.Grid(100, 50), MeshOptions.Grid(100, 50, P(0, 0)));
            Assert.NotEqual(MeshOptions.Strips(0.0), new MeshOptions(MeshKind.Strips));
            Assert.False(MeshOptions.Triangles.Equals(null));
        }

        [Fact]
        public void OptionsSayWhatTheyAre()
        {
            Assert.Equal("(Kind: Triangles, AngleRad: along the shape, ChordTolerance: automatic)", MeshOptions.Triangles.ToString());
            Assert.Equal("(Kind: Grid, Cell: 1200 x 600, Joint: 3, AngleRad: 0.5, Align: CenterCell/Start, ChordTolerance: 0.1)",
                new MeshOptions(MeshKind.Grid, 1200, 600, 3, 0.5, GridAlignment.CenterCell, chordTolerance: 0.1).ToString());
            Assert.StartsWith("(Kind: Grid, Cell: 10 x 10, Joint: 0, AngleRad: along the shape, Origin: ", MeshOptions.Grid(10, 10, P(1, 2)).ToString());
        }

        [Fact]
        public void ARectangleDividesIntoEqualRectanglesRowByRow()
        {
            var rectangle = new GeoRectangle2(P(1000, 500), 3000, 1200, 0.4);
            GeoRectangle2[] cells = rectangle.Divide(3, 2);

            Assert.Equal(6, cells.Length);
            Assert.All(cells, c => Assert.Equal(1000, c.Width, 9));
            Assert.All(cells, c => Assert.Equal(600, c.Height, 9));
            Assert.All(cells, c => Assert.Equal(0.4, c.AngleRad));
            Assert.Equal(rectangle.Area, cells.Sum(c => c.Area), 6);

            // The first from the lower left corner, the second along the width, the fourth along the height.
            var tight = new Tolerance(1E-9, 1E-9);
            Assert.True(cells[0].LowerLeft.IsEqualTo(rectangle.LowerLeft, tight));
            Assert.True(cells[0].LowerRight.IsEqualTo(cells[1].LowerLeft, tight));
            Assert.True(cells[0].UpperLeft.IsEqualTo(cells[3].LowerLeft, tight));
            Assert.True(cells[5].UpperRight.IsEqualTo(rectangle.UpperRight, tight));

            Assert.Single(rectangle.Divide(1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.Divide(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rectangle.Divide(1, -2));
        }

        [Fact]
        public void AKindAloneMakesEveryMeshButAGrid()
        {
            var square = new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10), P(0, 10));

            Assert.Equal(MeshKind.Triangles, square.ToMesh(MeshKind.Triangles).Kind);
            Assert.Equal(MeshKind.Strips, square.ToMesh(MeshKind.Strips).Kind);
            Assert.Equal(MeshKind.Convex, square.ToMesh(MeshKind.Convex).Kind);
            Assert.Throws<ArgumentException>(() => square.ToMesh(MeshKind.Grid));
            Assert.Throws<ArgumentNullException>(() => square.ToMesh((MeshOptions)null));
            Assert.Throws<ArgumentNullException>(() => Mesh2.ToMesh((GeoPolygon2)null, MeshOptions.Triangles));
        }
    }
}
