using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A handful of checks on the distances and shortest lines in space that neither side of a pair answered: flat
    /// regions against each other, a face against a segment, a chain, a ray and a body, a plane against a region and a
    /// ray, rays against rays and chains, and boxes through their bodies. The full suites live elsewhere; these only show
    /// the wiring holds.
    /// </summary>
    public class GroupDSmokeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z)
            => new GeoPolygon3(Pt(x0, y0, z), Pt(x1, y0, z), Pt(x1, y1, z), Pt(x0, y1, z));

        // A square standing upright along y = 50.
        private static GeoPolygon3 Upright(double x0, double x1, double z0, double z1)
            => new GeoPolygon3(Pt(x0, 50, z0), Pt(x1, 50, z0), Pt(x1, 50, z1), Pt(x0, 50, z1));

        // The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle.
        private static GeoFace3 Plate() => new GeoFace3(Square(0, 0, 100, 100, 0), new[] { Square(30, 30, 70, 70, 0) });

        private static void AssertLine(GeoLine3 actual, GeoPoint3 start, GeoPoint3 end)
        {
            Assert.True(actual.StartPoint.IsEqualTo(start, Tol), actual.StartPoint + " is not " + start);
            Assert.True(actual.EndPoint.IsEqualTo(end, Tol), actual.EndPoint + " is not " + end);
        }

        private static void AssertReversed(GeoLine3 line, GeoLine3 other)
        {
            Assert.Equal(line.StartPoint, other.EndPoint);
            Assert.Equal(line.EndPoint, other.StartPoint);
        }

        [Fact]
        public void ASegmentThroughAHoleIsJoinedToTheRimAndOneThroughTheMaterialMeetsIt()
        {
            GeoFace3 plate = Plate();
            var throughHole = new GeoLine3(Pt(45, 50, -10), Pt(45, 50, 10));
            var throughMaterial = new GeoLine3(Pt(20, 20, -10), Pt(20, 20, 10));

            AssertLine(throughHole.GetShortestLineTo(plate, Tol), Pt(45, 50, 0), Pt(30, 50, 0));
            Assert.Equal(15.0, throughHole.DistanceTo(plate, Tol), 12);
            AssertReversed(throughHole.GetShortestLineTo(plate, Tol), plate.GetShortestLineTo(throughHole, Tol));
            Assert.Equal(throughHole.DistanceTo(plate, Tol), plate.DistanceTo(throughHole, Tol));
            AssertLine(throughMaterial.GetShortestLineTo(plate, Tol), Pt(20, 20, 0), Pt(20, 20, 0));
            Assert.Equal(0.0, throughMaterial.DistanceTo(plate, Tol));
        }

        [Fact]
        public void AChainAndARayThroughAHoleAreJoinedToTheRim()
        {
            GeoFace3 plate = Plate();
            var chain = new GeoPolyline3(Pt(45, 50, 30), Pt(45, 50, -30));
            var ray = new GeoRay3(Pt(45, 50, 10), GeoVector3.ZAxis.Negate());

            AssertLine(chain.GetShortestLineTo(plate, Tol), Pt(45, 50, 0), Pt(30, 50, 0));
            AssertLine(ray.GetShortestLineTo(plate, Tol), Pt(45, 50, 0), Pt(30, 50, 0));
            Assert.Equal(15.0, plate.DistanceTo(chain, Tol), 12);
            Assert.Equal(15.0, plate.DistanceTo(ray, Tol), 12);
            AssertReversed(ray.GetShortestLineTo(plate, Tol), plate.GetShortestLineTo(ray, Tol));
            Assert.Equal(0.0, new GeoRay3(Pt(20, 20, 10), GeoVector3.ZAxis.Negate()).DistanceTo(plate, Tol));
        }

        [Fact]
        public void TwoSquaresApartAreJoinedFromAnEdgeAndCrossingTheyMeetAtTheFirstCrossing()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);

            AssertLine(floor.GetShortestLineTo(Upright(0, 100, 10, 110), Tol), Pt(100, 50, 0), Pt(100, 50, 10));
            Assert.Equal(10.0, floor.DistanceTo(Upright(0, 100, 10, 110), Tol), 12);

            GeoPolygon3 across = Upright(0, 100, -50, 50);
            GeoLine3 meeting = floor.GetShortestLineTo(across, Tol);

            Assert.Equal(0.0, meeting.Length);
            Assert.Equal(floor.GetIntersections(across, Tol)[0], meeting.StartPoint);
            Assert.Equal(0.0, floor.DistanceTo(across, Tol));
        }

        [Fact]
        public void ASquareLyingLevelWithinTheToleranceTouchesAndKeepsItsTrueGap()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            GeoPolygon3 above = Square(50, 50, 150, 150, 0.0004);

            Assert.True(floor.CollidesWith(above, Tol));
            Assert.Equal(0.0, floor.DistanceTo(above, Tol));
            Assert.Equal(0.0004, floor.GetShortestLineTo(above, Tol).Length, 12);
        }

        [Fact]
        public void ARegionInAHoleIsMeasuredToTheRimFromEitherSide()
        {
            GeoFace3 plate = Plate();
            GeoPolygon3 standing = Upright(40, 60, -10, 10);
            var lying = new GeoFace3(Square(40, 40, 60, 60, 0));
            var beside = new GeoTriangle3(Pt(110, 50, 0), Pt(200, 50, 0), Pt(110, 50, 100));

            AssertLine(plate.GetShortestLineTo(standing, Tol), Pt(70, 50, 0), Pt(60, 50, 0));
            AssertReversed(plate.GetShortestLineTo(standing, Tol), standing.GetShortestLineTo(plate, Tol));
            AssertLine(plate.GetShortestLineTo(lying, Tol), Pt(40, 30, 0), Pt(40, 40, 0));
            Assert.Equal(10.0, plate.DistanceTo(lying, Tol), 12);
            AssertLine(beside.GetShortestLineTo(plate, Tol), Pt(110, 50, 0), Pt(100, 50, 0));
            Assert.Equal(beside.DistanceTo(plate, Tol), plate.DistanceTo(beside, Tol));
            Assert.Equal(0.0, plate.DistanceTo(Square(5, 5, 25, 25, 0), Tol));
        }

        [Fact]
        public void ATriangleUnderASquareIsJoinedStraightUp()
        {
            var triangle = new GeoTriangle3(Pt(0, 0, 0), Pt(100, 0, 0), Pt(0, 100, 0));
            GeoPolygon3 square = Upright(0, 100, 10, 110);

            AssertLine(triangle.GetShortestLineTo(square, Tol), Pt(50, 50, 0), Pt(50, 50, 10));
            AssertReversed(triangle.GetShortestLineTo(square, Tol), square.GetShortestLineTo(triangle, Tol));
            Assert.Equal(10.0, square.DistanceTo(triangle, Tol), 12);
            Assert.Equal(0.0, triangle.DistanceTo(Upright(0, 100, -50, 50), Tol));
        }

        [Fact]
        public void APlaneIsMeasuredByTheHeightsOfTheCorners()
        {
            GeoPlane3 floor = GeoPlane3.XY;
            GeoPolygon3 standing = Upright(0, 100, 10, 110);
            var face = new GeoFace3(Upright(0, 100, 10, 110), new[] { Upright(30, 70, 40, 80) });
            var triangle = new GeoTriangle3(Pt(0, 50, -50), Pt(100, 50, -50), Pt(0, 50, 50));

            AssertLine(standing.GetShortestLineTo(floor, Tol), Pt(0, 50, 10), Pt(0, 50, 0));
            AssertLine(floor.GetShortestLineTo(face, Tol), Pt(0, 50, 0), Pt(0, 50, 10));
            Assert.Equal(10.0, floor.DistanceTo(standing, Tol));
            Assert.Equal(10.0, face.DistanceTo(floor, Tol));
            AssertLine(triangle.GetShortestLineTo(floor, Tol), Pt(50, 50, 0), Pt(50, 50, 0));
            Assert.Equal(0.0, floor.DistanceTo(triangle, Tol));
            AssertLine(Upright(0, 100, -50, 50).GetShortestLineTo(floor, Tol), Pt(100, 50, 0), Pt(100, 50, 0));
        }

        [Fact]
        public void ARayIsMeasuredToAPlaneFromItsOriginUnlessItRunsIntoIt()
        {
            GeoPlane3 floor = GeoPlane3.XY;
            var down = new GeoRay3(Pt(0, 0, 10), GeoVector3.ZAxis.Negate());
            var up = new GeoRay3(Pt(0, 0, 10), GeoVector3.ZAxis);
            var hair = new GeoRay3(Pt(0, 0, 0.0004), GeoVector3.ZAxis);

            AssertLine(down.GetShortestLineTo(floor, Tol), GeoPoint3.Origin, GeoPoint3.Origin);
            AssertLine(up.GetShortestLineTo(floor, Tol), Pt(0, 0, 10), GeoPoint3.Origin);
            AssertReversed(up.GetShortestLineTo(floor, Tol), floor.GetShortestLineTo(up, Tol));
            Assert.Equal(10.0, up.DistanceTo(floor, Tol));
            Assert.Equal(0.0, down.DistanceTo(floor, Tol));
            Assert.Equal(0.0, hair.DistanceTo(floor, Tol));
            Assert.Equal(0.0004, hair.GetShortestLineTo(floor, Tol).Length, 12);
            Assert.True(double.IsNaN(new GeoRay3(Pt(0, 0, double.NaN), GeoVector3.ZAxis).DistanceTo(floor, Tol)));
        }

        [Fact]
        public void TwoRaysAndARayAndAChainAreJoinedWhereTheyPassClosest()
        {
            var alongX = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);
            var alongY = new GeoRay3(Pt(50, 30, 40), GeoVector3.YAxis);
            var chain = new GeoPolyline3(Pt(0, 0, 0), Pt(100, 0, 0), Pt(100, 100, 0));
            var up = new GeoRay3(Pt(60, 50, 30), GeoVector3.ZAxis);

            AssertLine(alongX.GetShortestLineTo(alongY, Tol), Pt(50, 0, 0), Pt(50, 30, 40));
            Assert.Equal(50.0, alongX.DistanceTo(alongY, Tol), 12);
            Assert.Equal(0.0, alongX.DistanceTo(new GeoRay3(Pt(50, -50, 0), GeoVector3.YAxis), Tol));
            AssertLine(chain.GetShortestLineTo(up, Tol), Pt(100, 50, 0), Pt(60, 50, 30));
            AssertReversed(chain.GetShortestLineTo(up, Tol), up.GetShortestLineTo(chain, Tol));
            Assert.Equal(50.0, up.DistanceTo(chain, Tol), 12);
        }

        [Fact]
        public void ARayBesideASquareIsJoinedFromItsOrigin()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            var ray = new GeoRay3(Pt(130, 50, 40), GeoVector3.XAxis);

            AssertLine(ray.GetShortestLineTo(floor, Tol), Pt(130, 50, 40), Pt(100, 50, 0));
            AssertReversed(ray.GetShortestLineTo(floor, Tol), floor.GetShortestLineTo(ray, Tol));
            Assert.Equal(50.0, floor.DistanceTo(ray, Tol), 12);
        }

        [Fact]
        public void ABodyThroughAHoleIsMeasuredToTheRim()
        {
            GeoSolid3 box = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100)).ToObb().ToSolid();
            var ring = new GeoFace3(Square(-50, -50, 150, 150, 50), new[] { Square(-25, -25, 125, 125, 50) });

            Assert.Equal(25.0, box.DistanceTo(ring, Tol), 12);
            Assert.Equal(25.0, ring.GetShortestLineTo(box, Tol).Length, 12);
            AssertReversed(box.GetShortestLineTo(ring, Tol), ring.GetShortestLineTo(box, Tol));
            Assert.Equal(0.0, box.DistanceTo(new GeoFace3(Square(-50, -50, 150, 150, 50)), Tol));

            // Inside, the body is read solid through for the distance, and the segment runs out to its surface.
            var inside = new GeoFace3(Square(40, 40, 60, 60, 30));

            Assert.Equal(0.0, inside.DistanceTo(box, Tol));
            Assert.Equal(30.0, inside.GetShortestLineTo(box, Tol).Length, 12);
        }

        [Fact]
        public void ABoxIsMeasuredAsItsBody()
        {
            var aabb = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100));
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 body = obb.ToSolid();
            var segment = new GeoLine3(Pt(150, 50, 50), Pt(150, 50, 150));
            var ray = new GeoRay3(Pt(150, 50, 50), GeoVector3.XAxis);
            var plane = new GeoPlane3(Pt(0, 0, 130), GeoVector3.ZAxis);
            var triangle = new GeoTriangle3(Pt(0, 0, 130), Pt(100, 0, 130), Pt(0, 100, 130));
            GeoPolygon3 square = Square(0, 0, 100, 100, 130);
            var face = new GeoFace3(Square(-50, -50, 150, 150, 50), new[] { Square(-25, -25, 125, 125, 50) });

            Assert.Equal(body.DistanceTo(segment, Tol), obb.DistanceTo(segment, Tol));
            Assert.Equal(body.DistanceTo(segment, Tol), segment.DistanceTo(aabb, Tol));
            Assert.Equal(body.DistanceTo(ray, Tol), ray.DistanceTo(obb, Tol));
            Assert.Equal(body.DistanceTo(plane, Tol), aabb.DistanceTo(plane, Tol));
            Assert.Equal(body.DistanceTo(triangle, Tol), triangle.DistanceTo(obb, Tol));
            Assert.Equal(body.DistanceTo(square, Tol), square.DistanceTo(aabb, Tol));
            Assert.Equal(body.DistanceTo(face, Tol), obb.DistanceTo(face, Tol));
            Assert.Equal(30.0, plane.DistanceTo(obb, Tol), 12);

            var other = new GeoObb3(Pt(250, 50, 50), 100, 100, 100);

            Assert.Equal(obb.DistanceTo(other, Tol), aabb.DistanceTo(other, Tol));
            Assert.Equal(aabb.DistanceTo(other, Tol), other.DistanceTo(aabb, Tol));
            Assert.Throws<ArgumentNullException>("box", () => segment.DistanceTo((GeoObb3)null, Tol));
            Assert.Throws<InvalidOperationException>(() => segment.DistanceTo(GeoAabb3.Empty, Tol));
        }
    }
}
