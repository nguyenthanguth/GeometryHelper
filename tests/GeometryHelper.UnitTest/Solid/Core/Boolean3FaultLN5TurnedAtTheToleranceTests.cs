using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A chip, a slab or a gap exactly the tolerance thick on a turned body gives a union or a difference that is valid, of
    /// one reading or the other, as it did before openings were cut on rounding's terms.
    /// </summary>
    /// <remarks>
    /// A corner was read as on a plane within the planar tolerance and what rounding the coordinates can add, in every cut,
    /// and a face as in another's plane on the same terms; and a thin cell was read finer wherever it was thicker than the
    /// tolerance to the last bit. On a turned body a feature exactly the tolerance thick is that thick to a few bits either
    /// way, read one way at some corners and cells and the other at the rest: a cube turned and joined to a block standing
    /// 0.001 into its top came out open, a corner chip so joined wound two ways, the cube less a slab 0.001 deep
    /// in its top open by two edges 40 long, another turn's union failed, and a column joined to a beam 0.001 clear of it far
    /// from the origin held 150 cubic millimetres more than the two apart. Only the cutting of openings reads a corner so now,
    /// and a thin cell is read finer only where it is thicker than the tolerance and rounding.
    /// </remarks>
    public class Boolean3FaultLN5TurnedAtTheToleranceTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoTransform3 Turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1, GeoTransform3 turn)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid().TransformBy(turn);

        private static void AssertValidWithin(GeoSolid3 result, double low, double high)
        {
            SolidValidation3 validation = result.Validate(Default);
            Assert.True(validation.IsClosed, validation.ToString());
            Assert.True(validation.IsValid, validation.ToString());

            double volume = result.GetVolume(Default);
            Assert.InRange(volume, low, high);
        }

        [Fact]
        public void ACubeJoinedToABlockTheToleranceIntoItsTop_BothTurned_IsValidEitherWayRound()
        {
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100, Turn);
            GeoSolid3 block = Box(-20, 30, 99.999, 20, 60, 150, Turn);

            // The two share 0.6: taken once, or the block's sliver read as within the tolerance of nothing.
            Assert.True(cube.TryUnion(block, out GeoSolid3 one, Default));
            AssertValidWithin(one, 1060000.6 - 1E-6, 1060001.2 + 1E-6);
            Assert.True(block.TryUnion(cube, out GeoSolid3 other, Default));
            AssertValidWithin(other, 1060000.6 - 1E-6, 1060001.2 + 1E-6);
        }

        [Fact]
        public void ACubeJoinedToAChipTheToleranceIntoItsCorner_BothTurned_IsValidEitherWayRound()
        {
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100, Turn);
            GeoSolid3 chip = Box(-20, -20, 99.999, 20, 20, 150, Turn);

            Assert.True(cube.TryUnion(chip, out GeoSolid3 one, Default));
            AssertValidWithin(one, 1080001.2 - 1E-6, 1080001.6 + 1E-6);
            Assert.True(chip.TryUnion(cube, out GeoSolid3 other, Default));
            AssertValidWithin(other, 1080001.2 - 1E-6, 1080001.6 + 1E-6);
        }

        [Fact]
        public void ACubeLessASlabTheToleranceDeepInItsTop_BothTurned_IsValid()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(
                new GeoPoint3(32.51210343209659, 0, 0), new GeoVector3(0.1350835569366271, 0.4313071984477841, -0.4327822054204448), 4.569126457240957);

            Assert.True(Box(0, 0, 0, 100, 100, 100, turn).TrySubtract(Box(30, 30, 99.999, 70, 60, 100, turn), out GeoSolid3 left, Default));
            AssertValidWithin(left, 1000000.0 - 1.2 - 1E-6, 1000000.0 + 1E-6);
        }

        [Fact]
        public void ACubeJoinedToASlabTheToleranceDeepInItsTop_BothTurned_IsClosedEitherWayRound()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(
                new GeoPoint3(16.906434654680282, 0, 0), new GeoVector3(-0.260895405319005, 0.3250665966538091, -0.39196248922728116), 5.15051195824077);
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100, turn);
            GeoSolid3 slab = Box(30, 30, 99.999, 70, 60, 100, turn);

            // Closed, not valid: the union keeps the slab's sides as slivers, as it did before.
            Assert.True(cube.TryUnion(slab, out GeoSolid3 one, Default));
            Assert.True(one.Validate(Default).IsClosed);
            Assert.True(slab.TryUnion(cube, out GeoSolid3 other, Default));
            Assert.True(other.Validate(Default).IsClosed);
        }

        [Fact]
        public void AColumnJoinedToABeamTheToleranceClearOfIt_TurnedFarFromTheOrigin_HoldsOneReadingOrTheOther()
        {
            GeoTransform3 far = GeoTransform3.Translation(new GeoVector3(523456.7, 734567.8, 12345.6)) * Turn;
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 3000, far);
            GeoSolid3 beam = Box(300.001, 50, 2500, 5000, 250, 2900, far);

            // Apart, 645 999 920; the gap closed, 646 000 000.
            Assert.True(column.TryUnion(beam, out GeoSolid3 joined, Default));
            AssertValidWithin(joined, 645999920.0 - 1E-3, 646000000.0 + 1E-3);
        }
    }
}
