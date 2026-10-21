using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A slab thicker than the point tolerance encloses something to <see cref="GeoSolid3.Validate(Tolerance)"/>, as the
    /// booleans that cut and give one read it.
    /// </summary>
    /// <remarks>
    /// The volume was read against the tolerance times the area of the faces, and a slab t thick encloses half its faces'
    /// area times t: a slab up to twice the tolerance thick enclosed nothing, and the common part of a cube with a block
    /// running 0.0015 into its top, which the booleans cut since the chip is thicker than the tolerance, came back not valid.
    /// </remarks>
    public class SolidValidationFaultLN6SlabTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Theory]
        [InlineData(0.0015)]
        [InlineData(0.0019)]
        [InlineData(0.002)]
        public void ASlabThickerThanTheTolerance_IsValid(double thickness)
        {
            SolidValidation3 check = Box(0, 0, 0, 100, 100, thickness).Validate(Tol);

            Assert.True(check.IsValid);
            Assert.DoesNotContain(check.Issues, issue => issue.Kind == SolidIssueKind.NoVolume);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.001)]
        public void ASlabNoThickerThanTheTolerance_EnclosesNothing(double thickness)
        {
            SolidValidation3 check = Box(0, 0, 0, 100, 100, thickness).Validate(Tol);

            Assert.False(check.IsValid);
            Assert.Contains(check.Issues, issue => issue.Kind == SolidIssueKind.NoVolume);
        }

        [Theory]
        [InlineData(0.0015)]
        [InlineData(0.002)]
        public void TheCommonPartOfACubeAndAChipThickerThanTheTolerance_IsValid(double depth)
        {
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100);
            GeoSolid3 block = Box(30, 30, 100 - depth, 70, 60, 150);

            Assert.True(cube.TryIntersect(block, out GeoSolid3 chip, Tol));
            Assert.Equal(1200 * depth, chip.GetVolume(Tol), 6);
            Assert.True(chip.Validate(Tol).IsValid);
        }
    }
}
