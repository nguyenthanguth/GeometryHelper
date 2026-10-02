using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A face whose hole stands a hair off the boundary's plane, as a face from a modeller can: a point is placed against
    /// the hole in the face's plane, as it is against the boundary.
    /// </summary>
    public class FaceHoleOffFlatTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>A square of 1000 on z = 0 with a square hole of 200 in its middle, the hole's corners 0.0095 above.</summary>
        private static GeoFace3 Face()
        {
            var boundary = new GeoPolygon3(new[] { P(0, 0, 0), P(1000, 0, 0), P(1000, 1000, 0), P(0, 1000, 0) }, Tolerance);
            var hole = new GeoPolygon3(new[] { P(400, 400, 0.0095), P(400, 600, 0.0095), P(600, 600, 0.0095), P(600, 400, 0.0095) }, Tolerance);
            return new GeoFace3(boundary, new[] { hole }, Tolerance);
        }

        [Fact]
        public void APointInTheHoleIsOutsideTheFaceWhereverTheHoleStandsWithinTheTolerance()
        {
            // A thousandth below the face's plane, so 0.0105 from the hole's own: the face holds it within its tolerance,
            // and the hole is where it stands.
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(500, 500, -0.001), Tolerance));
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(500, 500, 0.0), Tolerance));
        }

        [Fact]
        public void APointOnTheHolesRimIsOnTheFacesSide()
        {
            Assert.Equal(PointLocation.OnSide, Face().Locate(P(400, 500, -0.001), Tolerance));
        }

        [Fact]
        public void APointOfTheMaterialIsInsideTheFace()
        {
            Assert.Equal(PointLocation.Inside, Face().Locate(P(200, 200, -0.001), Tolerance));
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(200, 200, -0.02), Tolerance));
        }
    }
}
