using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A point of a body whose first ray leaves through the wall of a hole and rises through the hole to meet the top a hair
    /// short of the rim: the top holds that crossing within the tolerance, which is as unreliable by a hole's rim as by the
    /// face's outer one, and the point is placed by another ray.
    /// </summary>
    public class RayGrazingHoleRimTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        [Fact]
        public void APointWhoseRayGrazesTheRimOfAHoleIsPlacedByAnother()
        {
            // A plate 1000 square and 200 thick. Its hole's far side runs from (400, 430) to (600, 620), nearly along the
            // ray's way in plan, (1, 1, 1): from the point, the ray leaves through the hole's wall at x = 400, rises through
            // the hole, and meets the top two and a half thousandths short of that side, 0.069 below where it would meet the
            // side's own wall.
            var outline = new GeoPolygon3(P(0, 0, 0), P(1000, 0, 0), P(1000, 1000, 0), P(0, 1000, 0));
            var hole = new GeoPolygon3(P(400, 400, 0), P(600, 400, 0), P(600, 620, 0), P(400, 430, 0));
            GeoSolid3 plate = GeoSolid3.Extrude(new GeoFace3(outline, new[] { hole }, Tolerance), new GeoVector3(0, 0, 200), Tolerance);

            Assert.Equal(PointLocation.Inside, plate.Locate(P(350, 375, 50.069), Tolerance));
            Assert.True(plate.Contains(P(350, 375, 50.069), Tolerance));
            Assert.Equal(PointLocation.OutSide, plate.Locate(P(450, 420, 50.069), Tolerance));
        }
    }
}
