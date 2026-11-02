using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Booleans of a cube of 100 with a hole 20 by 20 through it and a body clear of the hole: an opening of a body whose
    /// material the result does not keep, either of a common part or the one a difference takes away, shares no space with
    /// the other and is left out, so the boolean is the one of the cube without it; the hole of a body whose material is
    /// kept is cut into the result as before.
    /// </summary>
    public class Boolean3OpeningsApartTests
    {
        private static readonly Tolerance Tolerance = new Tolerance(0.001, 1E-5, 1E-5, 0.001);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        private static GeoSolid3 Cube(GeoSolid3 hole) => new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces, new[] { hole });

        private static GeoSolid3 Plain() => Box(0, 0, 0, 100, 100, 100);

        // A hole near one corner, its box 50 clear of the box of a block 40 by 20 by 20 standing 20 into the far side.
        private static GeoSolid3 HoleNearACorner() => Box(10, 10, -10, 30, 30, 110);

        private static GeoSolid3 Block() => Box(80, 40, 40, 120, 60, 60);

        // A bar 120 by 10 by 20 turned 45 degrees about z, its centre line running from (27.6, -12.4) to (112.4, 72.4)
        // through the cube's lower right, and a hole through the cube's upper left: the boxes of the two meet over 6 by 6,
        // but the hole stands 51.6 clear of the bar.
        private static GeoSolid3 TurnedBar()
            => Box(-60, -5, 30, 60, 5, 50).TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.ZAxis, System.Math.PI / 4))
                .TransformBy(GeoTransform3.Translation(new GeoVector3(70, 30, 0)));

        private static GeoSolid3 HoleUpperLeft() => Box(10, 70, -10, 30, 90, 110);

        [Fact]
        public void ACubeWithAHoleClearOfABox_JoinedToIt_HasTheHoleCutIn()
        {
            Assert.True(Boolean3.TryUnion(Cube(HoleNearACorner()), Block(), out GeoSolid3 union, Tolerance, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Empty(union.Openings);
            Assert.True(union.Validate(Tolerance).IsValid);
            Assert.InRange(union.GetVolume(Tolerance), 968000.0 - 1E-6, 968000.0 + 1E-6);
        }

        [Fact]
        public void ACubeWithAHoleClearOfABox_LessIt_HasTheHoleCutIn()
        {
            Assert.True(Boolean3.TrySubtract(Cube(HoleNearACorner()), Block(), out GeoSolid3 rest, Tolerance, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Empty(rest.Openings);
            Assert.True(rest.Validate(Tolerance).IsValid);
            Assert.InRange(rest.GetVolume(Tolerance), 952000.0 - 1E-6, 952000.0 + 1E-6);
        }

        [Fact]
        public void ABoxLessACubeWithAHoleClearOfIt_TakesNothingBackForTheHole()
        {
            Assert.True(Boolean3.TrySubtract(Block(), Cube(HoleNearACorner()), out GeoSolid3 rest, Tolerance, out BooleanOutcome outcome));

            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Empty(rest.Openings);
            Assert.True(rest.Validate(Tolerance).IsValid);
            Assert.InRange(rest.GetVolume(Tolerance), 8000.0 - 1E-6, 8000.0 + 1E-6);
        }

        [Fact]
        public void ATurnedBarLessACubeWithAHoleWhoseBoxMeetsItsButClearOfIt_IsCutAsLessTheCubeWithoutTheHole()
        {
            // The hole was cut into the cube first: the bar came apart into 5 cells, where it takes 3, and held a bit more.
            long before = Boolean3.CellsMade;
            Assert.True(Boolean3.TrySubtract(TurnedBar(), Cube(HoleUpperLeft()), out GeoSolid3 rest, Tolerance));
            long cells = Boolean3.CellsMade - before;

            before = Boolean3.CellsMade;
            Assert.True(Boolean3.TrySubtract(TurnedBar(), Plain(), out GeoSolid3 plain, Tolerance));
            long plainCells = Boolean3.CellsMade - before;

            Assert.True(rest.Validate(Tolerance).IsValid);
            Assert.Equal(plainCells, cells);
            Assert.Equal(plain.GetVolume(Tolerance), rest.GetVolume(Tolerance));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheCommonPartOfACubeWithAHoleClearOfATurnedBarAndTheBar_IsCutAsWithoutTheHole(bool barFirst)
        {
            // The hole's planes cut the cells as well: 5 cells, where 3 hold the same.
            GeoSolid3 cube = Cube(HoleUpperLeft()), bar = TurnedBar();

            long before = Boolean3.CellsMade;
            Assert.True(barFirst ? Boolean3.TryIntersect(bar, cube, out GeoSolid3 common, Tolerance) : Boolean3.TryIntersect(cube, bar, out common, Tolerance));
            long cells = Boolean3.CellsMade - before;

            before = Boolean3.CellsMade;
            Assert.True(barFirst ? Boolean3.TryIntersect(bar, Plain(), out GeoSolid3 plain, Tolerance) : Boolean3.TryIntersect(Plain(), bar, out plain, Tolerance));
            long plainCells = Boolean3.CellsMade - before;

            Assert.True(common.Validate(Tolerance).IsValid);
            Assert.Equal(plainCells, cells);
            Assert.Equal(plain.GetVolume(Tolerance), common.GetVolume(Tolerance));
        }
    }
}
