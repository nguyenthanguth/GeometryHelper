using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A bar whose centre line lies along a face of a part, within the tolerance of it, runs inside the part for as long
    /// as it lies on the face, as deep as the depth reads it: half the bar is in the material. It used to read the depth
    /// and no length inside at all.
    /// </summary>
    /// <remarks>
    /// The part is a box 1 000 by 200 by 100 from the origin, its top in z = 100; the bar is 800 long and 8 thick in
    /// radius, along X from x = -400 to 400 over the middle of the top, so half its length lies over the box.
    /// </remarks>
    public class ClashBarAlongAFaceTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double Radius = 8.0;

        private static GeoSolid3 Part()
            => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 200, 100)).ToObb().ToSolid();

        private static ClashResult Along(double height)
        {
            var bar = new ClashBar(new GeoPolyline3(new GeoPoint3(-400, 100, height), new GeoPoint3(400, 100, height)), Radius);

            return Clash3.Find(new[] { bar }, new[] { Part() }, ClashOptions.Default, Tol).Single();
        }

        [Theory]
        [InlineData(100.0)]
        [InlineData(100.0004)]
        [InlineData(99.9996)]
        public void ABarLyingAlongTheTopHalfOverTheBox_RunsInsideForHalfItsLength(double height)
        {
            ClashResult clash = Along(height);

            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.Equal(400.0, clash.LengthInside, 6);
            Assert.True(System.Math.Abs(clash.Depth - Radius) <= Tol.EqualPoint, $"{clash.Depth}");
        }

        [Fact]
        public void ABarLyingAlongAnEdgeOfTheBox_RunsInsideForAsLongAsItLiesOnTheEdge()
        {
            // Along the edge where the top meets the side y = 0, half its length over the box: a quarter of the bar is in
            // the material there, and its centre line lies on the surface for 400.
            var bar = new ClashBar(new GeoPolyline3(new GeoPoint3(-400, 0, 100), new GeoPoint3(400, 0, 100)), Radius);

            ClashResult clash = Clash3.Find(new[] { bar }, new[] { Part() }, ClashOptions.Default, Tol).Single();

            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.Equal(400.0, clash.LengthInside, 6);
            Assert.Equal(Radius, clash.Depth, 9);
        }

        [Theory]
        [InlineData(-200.0, 100.0012, 1300.0, 100.0006, 700.0)]
        [InlineData(-200.0, 100.001214286, 900.0, 100.000585714, 725.0)]
        public void ABarDriftingIntoTheToleranceOverTheTop_RunsInsideOnlyWhereItIsWithinIt(double x0, double z0, double x1, double z1, double expected)
        {
            // Along X over the middle of the top, from 0.0012 or so over it, beyond the tolerance, down to 0.0006 or so: it
            // comes within the tolerance at x = 300, or 175, and lies there up to the end of the box at x = 1 000, or its own
            // end at x = 900. It used to be counted whole wherever it ran over the box, 1 200 or 1 100.
            var bar = new ClashBar(new GeoPolyline3(new GeoPoint3(x0, 100, z0), new GeoPoint3(x1, 100, z1)), Radius);

            ClashResult clash = Clash3.Find(new[] { bar }, new[] { Part() }, ClashOptions.Default, Tol).Single();

            Assert.True(System.Math.Abs(clash.LengthInside - expected) <= Tol.EqualPoint, $"{clash.LengthInside}");
        }

        [Fact]
        public void ABarDriftingOutOfTheToleranceOfASide_RunsInsideOnlyWhereItIsWithinIt()
        {
            // Down the side y = 0 at x = 500 from z = 80 to -40, from 0.0002 off it to 0.0016: it leaves the tolerance 4/7 of
            // the way down, at z = 80 - 120 x 4/7, about 11.43, and runs within it for 120 x 4/7, about 68.57. It used to be
            // counted whole, 120.
            var bar = new ClashBar(new GeoPolyline3(new GeoPoint3(500, -0.0002, 80), new GeoPoint3(500, -0.0016, -40)), Radius);

            ClashResult clash = Clash3.Find(new[] { bar }, new[] { Part() }, ClashOptions.Default, Tol).Single();

            Assert.True(System.Math.Abs(clash.LengthInside - 120.0 * 4.0 / 7.0) <= Tol.EqualPoint, $"{clash.LengthInside}");
        }

        [Fact]
        public void ABarJustBeyondTheToleranceOverTheTop_StillRunsOutside()
        {
            // 0.002 over the top, twice the tolerance: the part reaches 7.998 into the bar, and the centre line stays out.
            ClashResult clash = Along(100.002);

            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.Equal(0.0, clash.LengthInside);
            Assert.Equal(Radius - 0.002, clash.Depth, 9);
        }
    }
}
