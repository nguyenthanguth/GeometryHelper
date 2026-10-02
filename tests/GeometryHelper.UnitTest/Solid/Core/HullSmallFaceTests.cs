using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A hull with a face smaller than a polygon of the tolerance may enclose: four corners of a body a boolean left, three
    /// of them a tenth of a millimetre apart.
    /// </summary>
    public class HullSmallFaceTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static readonly GeoPoint3[] Corners =
        {
            P(-604.1937711901, 319.94180133699774, 1140.7557802231779),
            P(-604.22877212151957, 319.928068063246, 1140.7557802231779),
            P(-604.1937711901, 319.83886389952136, 1140.7557802231779),
            P(-604.22877212151957, 319.928068063246, 1015.3680301226528),
        };

        [Fact]
        public void AHullWithAFaceSmallerThanTheToleranceIsMade()
        {
            Assert.True(ConvexHull3.TryOf(Corners, out GeoSolid3 hull, Tolerance));
            Assert.Equal(4, hull.Faces.Count);
            Assert.True(hull.GetSignedVolume() > 0.0);
            Assert.True(hull.IsClosed(Tolerance));
        }

        [Fact]
        public void ABoxIsFittedRoundCornersWhoseHullHasSuchAFace()
        {
            GeoObb3 box = GeoObb3.Fit(Corners, Tolerance);

            foreach (GeoPoint3 corner in Corners)
            {
                Assert.True(box.Contains(corner, Tolerance), $"{corner} is outside the box fitted round it");
            }
        }
    }
}
