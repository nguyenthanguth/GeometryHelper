using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A side of a straight polygon, polyline, face or triangle in the plane is the side it is however short to the closest
    /// point and the shortest line from a point, as it already was to the distance and as a side of a curved chain or loop
    /// is: a point beside a side no longer than the tolerance was carried to the side's start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The polygon is (0, 0), (10, 0), (10, 0.006), (0, 10), the polyline the hairpin from (0, 0) to (10, 0), on to (10, 0.006)
    /// and back to (0, 0.006); each has a side 0.006 long at x = 10 and is asked at a tolerance of a hundredth. The point
    /// (10.03, 0.003) lies 0.03 to the right of the middle of that side, where its corners are 0.0301496 away.
    /// </para>
    /// <para>
    /// The closest point of a triangle takes no tolerance and reads the global one, so it is asked of the triangle (0, 0),
    /// (10, 0), (10, 0.0006), whose side 0.0006 long is shorter than the default of a thousandth, from (10.03, 0.0003). No test
    /// sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1whTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint2 Beside = new GeoPoint2(10.03, 0.003);

        private static GeoPolygon2 Polygon()
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 10));

        private static GeoPolyline2 Hairpin()
            => new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 0.006));

        private static void AssertAt(double x, double y, GeoPoint2 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
        }

        [Fact]
        public void TheClosestPointOfAPolygonOrAPolyline_BesideItsShortSide_IsOnTheSideAsItIs()
        {
            // The foot on the side, 0.03 away, where it was the side's start (10, 0), 0.0301496 away.
            AssertAt(10, 0.003, Projection2.ProjectToPolygon(Polygon(), Beside, Hundredth));
            AssertAt(10, 0.003, Projection2.ProjectToPolyline(Hairpin(), Beside, Hundredth));
        }

        [Fact]
        public void TheShortestLineFromAPolygonOrAPolyline_ToAPointBesideItsShortSide_IsAsLongAsTheDistance()
        {
            GeoLine2 fromPolygon = Polygon().GetShortestLineTo(Beside, Hundredth);

            AssertAt(10, 0.003, fromPolygon.StartPoint);
            Assert.Equal(0.03, fromPolygon.Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(Beside, Hundredth).Length, 12);
            Assert.Equal(Hairpin().DistanceTo(Beside), Hairpin().GetShortestLineTo(Beside, Hundredth).Length, 12);
        }

        [Fact]
        public void TheClosestPointAndTheShortestLineOfAFace_BesideAShortSideOfItsOutlineOrOfAHole_AreOnTheSideAsItIs()
        {
            // The polygon as the outline of a face, and as a hole in the square 50 by 50 round it, the point on the material.
            var face = new GeoFace2(Polygon());
            var square = new GeoPolygon2(new GeoPoint2(-20, -20), new GeoPoint2(30, -20), new GeoPoint2(30, 30), new GeoPoint2(-20, 30));
            var holed = new GeoFace2(square, new[] { Polygon() });

            AssertAt(10, 0.003, face.GetClosestPointOnBoundary(Beside, Hundredth));
            AssertAt(10, 0.003, holed.GetClosestPointOnBoundary(Beside, Hundredth));
            Assert.Equal(0.03, face.GetShortestLineTo(Beside, Hundredth).Length, 12);
            Assert.Equal(0.03, holed.GetShortestLineTo(Beside, Hundredth).Length, 12);
        }

        [Fact]
        public void TheClosestPointOfATriangle_BesideASideShorterThanTheGlobalTolerance_IsOnThatSide()
        {
            // The foot (10, 0.0003), 0.03 away, where it was the side's start, 0.0300015 away.
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.0006));
            var point = new GeoPoint2(10.03, 0.0003);

            AssertAt(10, 0.0003, triangle.GetClosestPointOnBoundary(point));
            AssertAt(10, 0.0003, point.GetClosestPointOnBoundary(triangle));
        }
    }
}
