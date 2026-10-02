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

        [Fact]
        public void TheRimOfAHoleStandingOffTheFaceIsMeasuredInTheFacesPlane()
        {
            // The same plate, its top's hole and the tops of the hole's walls 0.02 above the top, within a planar tolerance of
            // 0.05: the ray meets the top two and a half thousandths short of the hole's side in the top's plane, where the top
            // holds it, but 0.02 from the side in space, past the band that throws another ray.
            var tolerance = new Tolerance(0.01, 0.01, System.Math.PI / 180, 0.05);
            GeoPoint3[] outer = { P(0, 0, 0), P(1000, 0, 0), P(1000, 1000, 0), P(0, 1000, 0) };
            GeoPoint3[] low = { P(400, 400, 0), P(600, 400, 0), P(600, 620, 0), P(400, 430, 0) };
            GeoPoint3[] high = System.Array.ConvertAll(low, p => P(p.X, p.Y, 200.02));
            var up = new GeoVector3(0, 0, 200);
            var faces = new System.Collections.Generic.List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { outer[0], outer[3], outer[2], outer[1] }, tolerance), new[] { new GeoPolygon3(new[] { low[0], low[3], low[2], low[1] }, tolerance) }, tolerance),
                new GeoFace3(new GeoPolygon3(System.Array.ConvertAll(outer, p => p.Add(up)), tolerance), new[] { new GeoPolygon3(high, tolerance) }, tolerance),
            };

            for (int k = 0; k < 4; k++)
            {
                GeoPoint3 a = outer[k], b = outer[(k + 1) % 4];
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { a, b, b.Add(up), a.Add(up) }, tolerance)));
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { low[(k + 1) % 4], low[k], high[k], high[(k + 1) % 4] }, tolerance)));
            }

            var plate = new GeoSolid3(faces);

            Assert.True(plate.IsClosed(tolerance));
            Assert.Equal(PointLocation.Inside, plate.Locate(P(350, 375, 50.069), tolerance));
            Assert.Equal(PointLocation.OutSide, plate.Locate(P(450, 420, 50.069), tolerance));
        }
    }
}
