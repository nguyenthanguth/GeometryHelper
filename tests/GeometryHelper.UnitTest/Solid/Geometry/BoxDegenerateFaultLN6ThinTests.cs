using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A box is flat where a whole side is no longer than the point tolerance, as its faces read it.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoObb3.IsDegenerate(Tolerance)"/> read half of each side against the point tolerance, so a plate 0.0015 or
    /// 0.002 thick, which has its six faces and is a body to every query, was called flat.
    /// </remarks>
    public class BoxDegenerateFaultLN6ThinTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Theory]
        [InlineData(0.0015)]
        [InlineData(0.002)]
        [InlineData(0.01)]
        public void APlateThickerThanTheTolerance_IsNotFlat(double thickness)
        {
            var plate = new GeoObb3(new GeoPoint3(0, 0, 0), 50, 30, thickness);

            Assert.False(plate.IsDegenerate(Tol));
            Assert.Equal(6, plate.GetFaces().Length);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.0005)]
        [InlineData(0.001)]
        public void APlateNoThickerThanTheTolerance_IsFlat(double thickness)
        {
            Assert.True(new GeoObb3(new GeoPoint3(0, 0, 0), 50, 30, thickness).IsDegenerate(Tol));
            Assert.True(new GeoObb3(new GeoPoint3(0, 0, 0), thickness, 50, 30).IsDegenerate(Tol));
        }
    }
}
