using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A rectangle thinner than the default tolerance keeps its four sides in the crossings and the shortest lines it
    /// has with an arc, a chain and a loop, as it does in their distances: it is not refused, and the answer is the one
    /// the polygon of its four corners gives, a polygon built where no corner is merged away.
    /// </summary>
    public class ThinRectangleCrossingsAndProjectionsTests
    {
        private const double Gap = 0.00175;

        private static GeoArc2 Arc() => new GeoArc2(new GeoPoint2(0, 0), 20, -Math.PI / 4.0, Math.PI / 4.0);

        private static GeoPolylineArc2 Chain() => new GeoPolylineArc2(
            new[] { new GeoPoint2(10, -10), new GeoPoint2(20, 0), new GeoPoint2(10, 10) },
            new[] { 0.0, 0.0, 0.0 });

        private static GeoPolygonArc2 Loop() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, -10), new GeoPoint2(20, -10), new GeoPoint2(20, 10), new GeoPoint2(0, 10) },
            new[] { 0.0, 0.0, 0.0, 0.0 });

        // 0.0005 wide and 10 tall, its left side at x = 20.00175, 0.00175 clear of the curves.
        private static GeoRectangle2 Apart() => new GeoRectangle2(new GeoPoint2(20.002, 0), 0.0005, 10, 0);

        // The same, standing across the curves at x = 20: its left side is at 19.99975 and its right at 20.00025.
        private static GeoRectangle2 Across() => new GeoRectangle2(new GeoPoint2(20, 0), 0.0005, 10, 0);

        // The polygon of the four corners, built where nothing is merged; the default tolerance would merge them to two.
        private static GeoPolygon2 Corners(GeoRectangle2 rect)
        {
            using (Tolerance.Use(new Tolerance(1E-9, Tolerance.DefaultEqualVector)))
            {
                return new GeoPolygon2(rect.GetVertices());
            }
        }

        private static void AssertSame(GeoPoint2[] expected, GeoPoint2[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].X, actual[i].X);
                Assert.Equal(expected[i].Y, actual[i].Y);
            }
        }

        [Fact]
        public void ARectangleHalfAThousandthWideAcrossAnArc_IsCrossedAsItsCornersAreCrossed()
        {
            // The circle of radius 20 meets the line x = 19.99975 at y = +-0.0999997, and the right side, outside it, is
            // within the point tolerance of it at (20.00025, 0): three points, as for the polygon of the corners.
            GeoRectangle2 rect = Across();
            GeoPolygon2 corners = Corners(rect);

            AssertSame(corners.GetIntersections(Arc()), rect.GetIntersections(Arc()));
            AssertSame(corners.GetIntersections(Arc()), Arc().GetIntersections(rect));
            Assert.Equal(3, rect.GetIntersections(Arc()).Length);
        }

        [Fact]
        public void ARectangleHalfAThousandthWideAcrossAChain_IsCrossedAsItsCornersAreCrossed()
        {
            // The two edges of the chain cross its left side 0.0005 apart, which the point tolerance reads as one.
            GeoRectangle2 rect = Across();
            GeoPolygon2 corners = Corners(rect);

            AssertSame(corners.GetIntersections(Chain()), rect.GetIntersections(Chain()));
            AssertSame(corners.GetIntersections(Chain()), Chain().GetIntersections(rect));
            Assert.Single(rect.GetIntersections(Chain()));
        }

        [Fact]
        public void ARectangleHalfAThousandthWideAcrossALoop_IsCrossedAsItsCornersAreCrossed()
        {
            // Its ends are shorter than the point tolerance, and a side that short is crossed by nothing, as for the polygon.
            GeoRectangle2 rect = Across();
            GeoPolygon2 corners = Corners(rect);

            AssertSame(corners.GetIntersections(Loop()), rect.GetIntersections(Loop()));
            AssertSame(corners.GetIntersections(Loop()), Loop().GetIntersections(rect));
        }

        [Fact]
        public void ARectangleHalfAThousandthWideApartFromAnArcAChainAndALoop_HasTheShortestLineItsSidesGive()
        {
            GeoRectangle2 rect = Apart();
            GeoPolygon2 corners = Corners(rect);

            Assert.InRange(rect.GetShortestLineTo(Arc()).Length, Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(Arc().GetShortestLineTo(rect).Length, Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.GetShortestLineTo(Chain()).Length, Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(Chain().GetShortestLineTo(rect).Length, Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.GetShortestLineTo(Loop()).Length, Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(Loop().GetShortestLineTo(rect).Length, Gap - 1E-9, Gap + 1E-9);

            Assert.Equal(corners.GetShortestLineTo(Arc()).Length, rect.GetShortestLineTo(Arc()).Length);
            Assert.Equal(corners.GetShortestLineTo(Chain()).Length, rect.GetShortestLineTo(Chain()).Length);
            Assert.Equal(corners.GetShortestLineTo(Loop()).Length, rect.GetShortestLineTo(Loop()).Length);
        }

        [Fact]
        public void ARectangleHalfAThousandthWide_IsEqualToItselfAndNotToAnotherATenthAway()
        {
            GeoRectangle2 rect = Apart();
            var moved = new GeoRectangle2(new GeoPoint2(20.1, 0), 0.0005, 10, 0);

            Assert.True(rect.IsEqualTo(rect));
            Assert.False(rect.IsEqualTo(moved));
        }
    }
}
