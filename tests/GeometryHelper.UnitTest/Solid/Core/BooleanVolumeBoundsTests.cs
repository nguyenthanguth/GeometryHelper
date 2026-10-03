using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// The volumes a boolean of two bodies can give, which hold what it gives within the tolerance times the area it can
    /// move across.
    /// </summary>
    /// <remarks>
    /// A plate 100 by 100 by 20 (200 000, its area 28 000) and a post 20 by 20 by 30 (12 000, its area 3 200), at the
    /// default tolerance: the post's area allows 32 either way. The results are boxes of the volume asked about.
    /// </remarks>
    public class BooleanVolumeBoundsTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A box 100 by 100 by the height that holds the volume.</summary>
        private static GeoSolid3 Holding(double volume) => Box(0, 0, 0, 100, 100, volume / 10000.0);

        private static readonly GeoSolid3 Plate = Box(0, 0, 0, 100, 100, 20);

        private static readonly GeoSolid3 Post = Box(40, 40, -5, 60, 60, 25);

        [Fact]
        public void ADifferenceLeavesNoMoreThanItsSubject_AndNoLessThanItLessTheWholeTool()
        {
            Assert.Null(Boolean3.Implausible("difference", Holding(200000 - 8000), Plate, Post, Tolerance));
            Assert.Null(Boolean3.Implausible("difference", Holding(200000 + 30), Plate, Post, Tolerance));
            Assert.Contains("no more than", Boolean3.Implausible("difference", Holding(200000 + 100), Plate, Post, Tolerance));
            Assert.Contains("no less than", Boolean3.Implausible("difference", Holding(200000 - 12000 - 100), Plate, Post, Tolerance));
        }

        [Fact]
        public void AUnionHoldsTheLargerAndNoMoreThanBoth()
        {
            Assert.Null(Boolean3.Implausible("union", Holding(200000 + 4000), Plate, Post, Tolerance));
            Assert.Contains("no less than", Boolean3.Implausible("union", Holding(200000 - 1000), Plate, Post, Tolerance));
            Assert.Contains("no more than", Boolean3.Implausible("union", Holding(212000 + 1000), Plate, Post, Tolerance));
        }

        [Fact]
        public void ACommonPartHoldsNoMoreThanTheSmaller()
        {
            Assert.Null(Boolean3.Implausible("intersection", Holding(8000), Plate, Post, Tolerance));
            Assert.Contains("no more than", Boolean3.Implausible("intersection", Holding(12000 + 1000), Plate, Post, Tolerance));
        }

        [Fact]
        public void ABodyWithOpeningsIsHeldFromAboveOnly()
        {
            // Its faces hold more than its material, so a difference may leave much less than they do.
            GeoSolid3 pierced = Plate.WithOpenings(new[] { Box(10, 10, -1, 90, 90, 21) });

            Assert.Null(Boolean3.Implausible("difference", Box(0, 0, 0, 10, 10, 1), pierced, Post, Tolerance));
            Assert.Contains("no more than", Boolean3.Implausible("difference", Holding(200000 + 100), pierced, Post, Tolerance));
        }
    }
}
