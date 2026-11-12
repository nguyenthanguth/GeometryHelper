using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment lying in the plane of a triangle, a polygon or a face and crossing its outline collides with it and is
    /// given no point, as documented: they meet along a stretch, which names no one place. An arc lying in the plane is
    /// given the points where it cuts the outline.
    /// </summary>
    public class SegmentLyingInARegionsPlaneTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly GeoPolygon3 Square = new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100, 100, 0),
            new GeoPoint3(0, 100, 0)
        );

        [Fact]
        public void ASegmentInThePlaneCrossingTheOutline_CollidesAndIsGivenNoPoint()
        {
            var line = new GeoLine3(new GeoPoint3(-50, 20, 0), new GeoPoint3(50, 20, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            Assert.True(line.CollidesWith(Square, Tol));
            Assert.Empty(line.GetIntersections(Square, Tol));
            Assert.True(line.CollidesWith(triangle, Tol));
            Assert.Empty(line.GetIntersections(triangle, Tol));
            Assert.Empty(line.GetIntersections(new GeoFace3(Square), Tol));
        }

        [Fact]
        public void AnArcInThePlane_IsGivenWhereItCutsTheOutline()
        {
            var arc = GeoArc3.FromThreePoints(
                new GeoPoint3(-20, 50, 0),
                new GeoPoint3(0, 70, 0),
                new GeoPoint3(20, 50, 0)
            );

            GeoPoint3[] points = arc.GetIntersections(Square, Tol);

            Assert.Single(points);
            Assert.True(points[0].IsEqualTo(new GeoPoint3(0, 70, 0), Tol));
        }
    }
}
