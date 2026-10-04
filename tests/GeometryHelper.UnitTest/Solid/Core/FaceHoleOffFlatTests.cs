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

        /// <summary>A square of 1000 on z = 0 with a square hole of 200 in its middle, the hole's corners 0.00095 above.</summary>
        private static GeoFace3 Face()
        {
            var boundary = new GeoPolygon3(new[] { P(0, 0, 0), P(1000, 0, 0), P(1000, 1000, 0), P(0, 1000, 0) }, Tolerance);
            var hole = new GeoPolygon3(new[] { P(400, 400, 0.00095), P(400, 600, 0.00095), P(600, 600, 0.00095), P(600, 400, 0.00095) }, Tolerance);
            return new GeoFace3(boundary, new[] { hole }, Tolerance);
        }

        [Fact]
        public void APointInTheHoleIsOutsideTheFaceWhereverTheHoleStandsWithinTheTolerance()
        {
            // A ten-thousandth below the face's plane, so 0.00105 from the hole's own: the face holds it within its
            // tolerance, and the hole is where it stands.
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(500, 500, -0.0001), Tolerance));
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(500, 500, 0.0), Tolerance));
        }

        [Fact]
        public void APointOnTheHolesRimIsOnTheFacesSide()
        {
            Assert.Equal(PointLocation.OnSide, Face().Locate(P(400, 500, -0.0001), Tolerance));
        }

        [Fact]
        public void APointOfTheMaterialIsInsideTheFace()
        {
            Assert.Equal(PointLocation.Inside, Face().Locate(P(200, 200, -0.0001), Tolerance));
            Assert.Equal(PointLocation.OutSide, Face().Locate(P(200, 200, -0.002), Tolerance));
        }

        [Fact]
        public void TheMaterialNearestAPointOverTheHoleIsOnItsRim()
        {
            // A hole whose corners stand 0.00095, 0.00095, 0.00095 and -0.00047 off the face, and a point on the face 2 into it:
            // its distance and its nearest point read the hole in the hole's own plane, which held nothing of it there, and
            // gave the foot of the perpendicular, in the hole.
            var boundary = new GeoPolygon3(new[] { P(0, 0, 10), P(100, 0, 10), P(100, 100, 10), P(0, 100, 10) }, Tolerance);
            var hole = new GeoPolygon3(new[] { P(40, 50, 10.00095), P(60, 50, 10.00095), P(60, 60, 10.00095), P(40, 60, 9.99953) }, Tolerance);
            var face = new GeoFace3(boundary, new[] { hole }, Tolerance);
            GeoPoint3 over = P(50, 52, 10);

            Assert.Equal(PointLocation.OutSide, face.Locate(over, Tolerance));
            Assert.Equal(2.0, face.DistanceTo(over, Tolerance), 2);
            Assert.Equal(50.0, face.GetClosestPointOnBoundary(over, Tolerance).Y, 2);
            // Two above it, the nearest material is the rim, which stands 0.00095 above the face there.
            Assert.Equal(System.Math.Sqrt(4 + 1.99905 * 1.99905), face.DistanceTo(P(50, 52, 12), Tolerance), 6);
        }

        [Fact]
        public void APointInAHoleThroughAPlateIsOutOfThePlate()
        {
            // A plate 100 by 100 by 10 with a hole through it at x 40..60, y 50..60, the hole's top corners standing 0.00095,
            // 0.00095, 0.00095 and -0.00047 off the top: a point 2 into the hole at the level of the top was taken for one on
            // the top face, so on the plate's side.
            GeoPoint3[] outer = { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0) };
            GeoPoint3[] low = { P(40, 50, 0), P(60, 50, 0), P(60, 60, 0), P(40, 60, 0) };
            GeoPoint3[] high = { P(40, 50, 10.00095), P(60, 50, 10.00095), P(60, 60, 10.00095), P(40, 60, 9.99953) };
            var up = new GeoVector3(0, 0, 10);
            var faces = new System.Collections.Generic.List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { outer[0], outer[3], outer[2], outer[1] }, Tolerance), new[] { new GeoPolygon3(new[] { low[0], low[3], low[2], low[1] }, Tolerance) }, Tolerance),
                new GeoFace3(new GeoPolygon3(System.Array.ConvertAll(outer, p => p.Add(up)), Tolerance), new[] { new GeoPolygon3(high, Tolerance) }, Tolerance),
            };

            for (int k = 0; k < 4; k++)
            {
                GeoPoint3 a = outer[k], b = outer[(k + 1) % 4];
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { a, b, b.Add(up), a.Add(up) }, Tolerance)));
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { low[(k + 1) % 4], low[k], high[k], high[(k + 1) % 4] }, Tolerance)));
            }

            var plate = new GeoSolid3(faces);
            GeoPoint3 inHole = P(50, 52, 10);

            Assert.True(plate.IsClosed(Tolerance));
            Assert.Equal(PointLocation.OutSide, plate.Locate(inHole, Tolerance));
            Assert.False(plate.Contains(inHole, Tolerance));
            Assert.Equal(2.0, plate.DistanceTo(inHole, Tolerance), 2);
        }
    }
}
