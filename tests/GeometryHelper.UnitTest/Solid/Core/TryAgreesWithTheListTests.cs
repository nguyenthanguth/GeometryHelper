using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// <c>TryIntersectWith</c> of a ray against a triangle, a polygon or a face gives the first point <c>GetIntersections</c>
    /// gives, and false where that gives none. A ray lying level over a shape was given its crossing of the plane by the one
    /// while the other, reading the ray as its line, gave nothing; and a ray passing the rim within the tolerance at a slant
    /// was given the touch by the list and refused by the one.
    /// </summary>
    public class TryAgreesWithTheListTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

        private static readonly GeoPolygon3 Square = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

        private static void SamePoint(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
            Assert.Equal(expected.Z, actual.Z);
        }

        [Theory]
        [InlineData(100.0, 0.0002, 4E-6)]
        [InlineData(100.0, 0.00005, 1E-6)]
        [InlineData(5000.0, 1E-7, 1E-10)]
        public void ARayLyingLevelOverTheShapeWithinThePlanarTolerance_IsRefused_AsTheListGivesNothing(double length, double height, double fall)
        {
            // From (10, 50) a hair over the floor, falling along X: it would cross the floor at (60, 50, 0) or (1010, 50, 0),
            // but it climbs less than the planar tolerance across the shape and lies level over it, as its line does.
            var rectangle = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(length, 0, 0), new GeoPoint3(length, 100, 0), new GeoPoint3(0, 100, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(2 * length, 0, 0), new GeoPoint3(0, 200, 0));
            var ray = new GeoRay3(new GeoPoint3(10, 50, height), new GeoVector3(1, 0, -fall).Normalize());

            Assert.Empty(ray.GetIntersections(rectangle, Thousandth));
            Assert.False(ray.TryIntersectWith(rectangle, out _, Thousandth));
            Assert.Empty(ray.GetIntersections(new GeoFace3(rectangle), Thousandth));
            Assert.False(ray.TryIntersectWith(new GeoFace3(rectangle), out _, Thousandth));
            Assert.Empty(ray.GetIntersections(triangle, Thousandth));
            Assert.False(ray.TryIntersectWith(triangle, out _, Thousandth));
            Assert.False(triangle.TryIntersectWith(ray, out _, Thousandth));
        }

        [Fact]
        public void ARayPassingTheRimWithinTheToleranceAtASlant_IsGivenTheTouch_AsTheListGivesIt()
        {
            // From (50, -1.002, -0.5) along (0, 1, 0.5): it crosses the floor 0.002 outside the side y = 0, but passes it 0.00089
            // away at (50, -0.0004, 0.0008), within the tolerance, and the list gives that touch.
            var ray = new GeoRay3(new GeoPoint3(50, -1.002, -0.5), new GeoVector3(0, 1, 0.5).Normalize());
            var face = new GeoFace3(Square);

            GeoPoint3 touch = Assert.Single(ray.GetIntersections(Square, Thousandth));
            Assert.InRange(touch.DistanceTo(new GeoPoint3(50, -0.0004, 0.0008)), 0.0, 1E-6);

            Assert.True(ray.TryIntersectWith(Square, out GeoPoint3 point, Thousandth));
            SamePoint(touch, point);
            Assert.True(Square.TryIntersectWith(ray, out point, Thousandth));
            SamePoint(touch, point);
            Assert.True(ray.TryIntersectWith(face, out point, Thousandth));
            SamePoint(Assert.Single(ray.GetIntersections(face, Thousandth)), point);
            Assert.True(ray.TryIntersectWith(Triangle, out point, Thousandth));
            SamePoint(Assert.Single(ray.GetIntersections(Triangle, Thousandth)), point);
        }

        [Fact]
        public void APlainCrossing_IsGivenAsItWas()
        {
            // Straight down from (20, 20, 10): (20, 20, 0) by every reading.
            var ray = new GeoRay3(new GeoPoint3(20, 20, 10), new GeoVector3(0, 0, -1));
            var crossing = new GeoPoint3(20, 20, 0);

            Assert.True(ray.TryIntersectWith(Triangle, out GeoPoint3 point, Thousandth));
            SamePoint(crossing, point);
            Assert.True(ray.TryIntersectWith(Square, out point, Thousandth));
            SamePoint(crossing, point);
            Assert.True(ray.TryIntersectWith(new GeoFace3(Square), out point, Thousandth));
            SamePoint(crossing, point);
        }

        [Fact]
        public void ARayStraightDownThroughAHole_CrossesNothing()
        {
            var face = new GeoFace3(Square, new[] { new GeoPolygon3(new GeoPoint3(30, 30, 0), new GeoPoint3(70, 30, 0), new GeoPoint3(70, 70, 0), new GeoPoint3(30, 70, 0)) });
            var ray = new GeoRay3(new GeoPoint3(50, 50, 10), new GeoVector3(0, 0, -1));

            Assert.False(ray.TryIntersectWith(face, out _, Thousandth));
            Assert.Empty(ray.GetIntersections(face, Thousandth));
        }

        [Fact]
        public void ASegmentOrALine_IsGivenByTryNoPointTheListRefuses()
        {
            // A segment or a line is crossed by Try only where it crosses the plane within the shape, and the list gives that
            // crossing too; the list gives a touch besides. The line through (10, 50, 0.0002) falling 4E-6 lies level over the
            // square and is given nothing by either, and the segment from (50, -1.002, -0.5) to (50, 0.998, 0.5), crossing the
            // floor 0.002 outside the side y = 0 and passing it 0.00089 away, is given that touch by the list alone.
            GeoVector3 falling = new GeoVector3(1, 0, -4E-6).Normalize();
            var line = new GeoXLine3(new GeoPoint3(10, 50, 0.0002), falling);
            var segment = new GeoLine3(new GeoPoint3(50, -1.002, -0.5), new GeoPoint3(50, 0.998, 0.5));

            Assert.Empty(line.GetIntersections(Square, Thousandth));
            Assert.False(line.TryIntersectWith(Square, out _, Thousandth));
            Assert.InRange(Assert.Single(segment.GetIntersections(Square, Thousandth)).DistanceTo(new GeoPoint3(50, -0.0004, 0.0008)), 0.0, 1E-6);
            Assert.False(segment.TryIntersectWith(Square, out _, Thousandth));
        }
    }
}
