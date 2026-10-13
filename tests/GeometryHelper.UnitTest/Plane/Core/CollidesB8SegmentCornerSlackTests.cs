using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two segments collide exactly when they come within the point tolerance of each other, and measure nought exactly
    /// where they truly cross or overlap.
    /// </summary>
    /// <remarks>
    /// The crossing test allows the crossing of the two lines up to the point tolerance past the end of either segment,
    /// so that splitting and trimming can place it on both. Read as a collision, two corners up to twice the tolerance
    /// apart touched: a segment ending at (10, 0) and one leaving (10.00135, 0.00078) at 60 degrees, 0.00156 apart,
    /// collided at a thousandth and measured nought. So did every pair that took a crossing from that test as a
    /// collision: rectangle, polygon and polyline against a segment, polygon against polygon and rectangle against
    /// polygon (F2, Q2). The crossing test itself keeps its slack.
    /// </remarks>
    public class CollidesB8SegmentCornerSlackTests
    {
        /// <summary>
        /// The exact distance of the two corners, 0.0027 / sqrt 3: from (10, 0) to (10 + 0.00135, 0.00135 / sqrt 3).
        /// </summary>
        private const double CornerGap = 0.0015588457268119896;

        /// <summary>The segment from the origin to (10, 0).</summary>
        private static GeoLine2 Along() => new GeoLine2(P(0, 0), P(10, 0));

        /// <summary>
        /// The segment 10 long leaving (10.00135, 0.00135 / sqrt 3) at 60 degrees. Its line crosses the X axis 0.0009 back,
        /// at (10.0009, 0): 0.0009 past the end of <see cref="Along"/> and 0.0009 before its own start, both within a
        /// thousandth. The nearest pair is the two ends, <see cref="CornerGap"/> apart: the end of the first projects
        /// before the start of the second, and the start of the second past the end of the first.
        /// </summary>
        private static GeoLine2 Leaving() => new GeoLine2(P(10.00135, 0.00077942286340599), P(15.00135, 8.66102540378444));

        [Fact]
        public void TwoCornersOneAndAHalfThousandthsApart_DoNotCollideAtAThousandth()
        {
            // The audit's repro: true on 2755f30, from either side and through the static form.
            GeoLine2 a = Along(), b = Leaving();

            Assert.False(Collision2.CollidesWith(a, b, Thousandth));
            Assert.False(Collision2.CollidesWith(b, a, Thousandth));
            Assert.False(a.CollidesWith(b, Thousandth));
            Assert.False(b.CollidesWith(a, Thousandth));
        }

        [Fact]
        public void TwoCornersOneAndAHalfThousandthsApart_MeasureTheirGapAtEveryTolerance()
        {
            // Nought on 2755f30 at 1E-3 and 0.1, where the slack let the lines' crossing count as a touch.
            GeoLine2 a = Along(), b = Leaving();

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.Equal(CornerGap, Distance2.DistanceTo(a, b, tolerance), 1E-12);
                Assert.Equal(CornerGap, Distance2.DistanceTo(b, a, tolerance), 1E-12);
                Assert.Equal(CornerGap, a.DistanceTo(b, tolerance), 1E-12);
            }
        }

        [Fact]
        public void TwoCornersOneAndAHalfThousandthsApart_CollideAtATenthAndAtTwoThousandths()
        {
            // Guards: the gap is within these.
            GeoLine2 a = Along(), b = Leaving();
            var twoThousandths = new GeometryHelper.Tolerance(2E-3, 1E-5);

            Assert.True(a.CollidesWith(b, Tenth));
            Assert.True(b.CollidesWith(a, Tenth));
            Assert.True(a.CollidesWith(b, twoThousandths));
            Assert.True(b.CollidesWith(a, twoThousandths));
            Assert.False(a.CollidesWith(b, TenThousandth));
        }

        [Fact]
        public void TwoCornersFiveTenThousandthsApart_CollideAtAThousandthAndMeasureTheirGap()
        {
            // The segment leaving (10.0005, 0) at 60 degrees: 0.0005 from the end of the first, and crossing nothing, so
            // its distance is the gap, not nought. On 2755f30 the crossing at its own start, 0.0005 past the first's end,
            // counted and the distance read nought.
            GeoLine2 a = Along();
            var b = new GeoLine2(P(10.0005, 0), P(15.0005, 5 * Math.Sqrt(3)));

            Assert.True(a.CollidesWith(b, Thousandth));
            Assert.True(b.CollidesWith(a, Thousandth));
            Assert.Equal(0.0005, a.DistanceTo(b, Thousandth), 1E-12);
            Assert.Equal(0.0005, b.DistanceTo(a, Thousandth), 1E-12);
        }

        [Fact]
        public void TwoSegmentsMeetingAtAnEndOrCrossing_StillCollideAndMeasureNought()
        {
            // Guards: a T, a true crossing, and an overlap along a line.
            GeoLine2 a = Along();
            var t = new GeoLine2(P(10, -1), P(10, 1));
            var across = new GeoLine2(P(5, -1), P(5, 1));
            var over = new GeoLine2(P(8, 0), P(12, 0));

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                foreach (GeoLine2 other in new[] { t, across, over })
                {
                    Assert.True(a.CollidesWith(other, tolerance));
                    Assert.True(other.CollidesWith(a, tolerance));
                    Assert.Equal(0.0, a.DistanceTo(other, tolerance));
                    Assert.Equal(0.0, other.DistanceTo(a, tolerance));
                }
            }
        }

        [Fact]
        public void TheCrossingTest_KeepsItsSlackPastTheEnds()
        {
            // A guard: TryIntersectWith places crossings for splitting and trimming, and is not a collision test. The
            // lines cross at (10.0009, 0), 0.0009 past the first segment's end; the point is placed on that segment, at
            // its end.
            Assert.True(Intersection2.TryIntersectWith(Along(), Leaving(), out GeoPoint2 at, Thousandth));
            Assert.Equal(10.0, at.X, 1E-12);
            Assert.Equal(0.0, at.Y, 1E-12);
        }

        [Fact]
        public void ARayLeavingACornerOneAndAHalfThousandthsAway_DoesNotCollideAtAThousandth()
        {
            // The ray from the start of Leaving, the same way: it reads the segment test through its cut.
            var ray = new GeoRay2(P(10.00135, 0.00077942286340599), U(1, Math.Sqrt(3)));
            GeoLine2 a = Along();

            Assert.False(ray.CollidesWith(a, Thousandth));
            Assert.False(a.CollidesWith(ray, Thousandth));
            Assert.Equal(CornerGap, ray.DistanceTo(a, Thousandth), 1E-12);
        }

        [Fact]
        public void APolylineEndingAtTheFirstCorner_DoesNotCollideWithTheSegmentFromTheOther()
        {
            // The polyline forms go edge by edge through the segment's.
            var polyline = new GeoPolyline2(P(0, -5), P(0, 0), P(10, 0));

            Assert.False(polyline.CollidesWith(Leaving(), Thousandth));
            Assert.False(Leaving().CollidesWith(polyline, Thousandth));
        }

        [Fact]
        public void TwoTrianglesWithCornersOneAndAHalfThousandthsApart_MeasureTheirGap()
        {
            // The triangles built on the two segments, (0, 0), (10, 0), (5, -5) and Leaving closed through (20, 0.5): their
            // distance is the corners' gap. On 2755f30 nought, from the collision on the global tolerance.
            var below = new GeoPolygon2(P(0, 0), P(10, 0), P(5, -5));
            var above = new GeoPolygon2(P(10.00135, 0.00077942286340599), P(15.00135, 8.66102540378444), P(20, 0.5));

            Assert.Equal(CornerGap, below.DistanceTo(above), 1E-12);
            Assert.Equal(CornerGap, above.DistanceTo(below), 1E-12);
        }

        [Fact]
        public void APolygonWithTwoCornersOneAndAQuarterThousandthsApart_IsSimpleAtAThousandthButNotAtTwo()
        {
            // The loop (0, 0), (10, 0), (10, -5), (20, -5), (20, 12), (11.0009, 9.0009), (10.0009, 0.0009), (0, 10). Its
            // corner at (10, 0) and its corner at (10.0009, 0.0009) are 0.0009 sqrt 2 = 0.00127 apart, and that is the
            // distance of every pair of edges that are not neighbours and come near (the first or second against the
            // sixth or seventh); none touch. The two corners lie within a thousandth of each other on X and on Y, so the
            // sweep of IsSimple compares them. The first edge's line and the sixth's, of slope 9, cross at (10.0008, 0):
            // 0.0008 past the first's end and 0.0009 before the sixth's, both within the slack, which on 2755f30 made it
            // not simple at 1E-3. It goes through Collision2.CollidesWith(segment, segment). (The corners of Leaving are
            // 0.00135 apart on X, so the sweep never compares them.) At 2E-3 the corners are within the tolerance.
            var polygon = new GeoPolygon2(
                P(0, 0), P(10, 0), P(10, -5), P(20, -5), P(20, 12), P(11.0009, 9.0009), P(10.0009, 0.0009), P(0, 10));

            Assert.True(polygon.IsSimple(Thousandth));
            Assert.True(polygon.IsSimple(TenThousandth));
            Assert.False(polygon.IsSimple(new GeometryHelper.Tolerance(2E-3, 1E-5)));
        }

        [Fact]
        public void TwoCornersOneAndAQuarterThousandthsApartWithinTheSlackOnBothAxes_DoNotCollideAtAThousandth()
        {
            // The first and sixth edges of the loop above, alone: 0.0009 sqrt 2 apart.
            GeoLine2 a = Along();
            var b = new GeoLine2(P(10.0009, 0.0009), P(11.0009, 9.0009));

            Assert.False(a.CollidesWith(b, Thousandth));
            Assert.False(b.CollidesWith(a, Thousandth));
            Assert.Equal(0.0009 * Math.Sqrt(2), a.DistanceTo(b, Thousandth), 1E-12);
        }

        #region F2 (Q2): the shapes that decide by TryIntersectWith with its slack

        private static GeoPolygon2 SquarePolygon() => new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));

        private static GeoRectangle2 SquareRectangle() => new GeoRectangle2(P(50, 50), 100, 100);

        /// <summary>
        /// <see cref="Leaving"/> moved onto the square's corner (100, 100): it leaves (100.00135, 100.00078) at 60 degrees,
        /// <see cref="CornerGap"/> from the corner, its line crossing the top side's at (100.0009, 100), within the slack of
        /// both.
        /// </summary>
        private static GeoLine2 LeavingTheCorner() => new GeoLine2(P(100.00135, 100.00077942286340599), P(105.00135, 108.66102540378444));

        /// <summary>
        /// The triangle with its corner where <see cref="LeavingTheCorner"/> starts, its sides leaving at 60 and 31.4
        /// degrees: the second one's line misses the top side's slack (0.0015 back from the corner).
        /// </summary>
        private static GeoPolygon2 TriangleAtTheCorner()
        {
            GeoLine2 side = LeavingTheCorner();
            GeoVector2 dir = U(1, Math.Sqrt(3)), turned = U(Math.Cos(Math.PI / 3 - 0.5), Math.Sin(Math.PI / 3 - 0.5));

            return new GeoPolygon2(side.StartPoint, side.StartPoint.Add(dir * 10), side.StartPoint.Add(turned * 10));
        }

        [Fact]
        public void ASegmentLeavingOneAndAHalfThousandthsFromASquaresCorner_DoesNotCollideWithItAtAThousandth()
        {
            // The polygon (Collision2.cs:292) and the rectangle (:216, what Arranging asks of a line obstacle); true on
            // 2755f30.
            GeoLine2 segment = LeavingTheCorner();

            Assert.False(SquarePolygon().CollidesWith(segment, Thousandth));
            Assert.False(segment.CollidesWith(SquarePolygon(), Thousandth));
            Assert.False(Collision2.CollidesWith(SquareRectangle(), segment, Thousandth));
            Assert.False(segment.CollidesWith(SquareRectangle(), Thousandth));
            Assert.True(segment.CollidesWith(SquarePolygon(), new GeometryHelper.Tolerance(2E-3, 1E-5)));
        }

        [Fact]
        public void ATriangleWhoseCornerIsOneAndAHalfThousandthsFromASquaresCorner_DoesNotCollideWithItAtAThousandth()
        {
            // Polygon against polygon (:321) and the rectangle against the polygon (:251).
            GeoPolygon2 triangle = TriangleAtTheCorner();

            Assert.False(SquarePolygon().CollidesWith(triangle, Thousandth));
            Assert.False(triangle.CollidesWith(SquarePolygon(), Thousandth));
            Assert.False(SquareRectangle().CollidesWith(triangle, Thousandth));
            Assert.False(triangle.CollidesWith(SquareRectangle(), Thousandth));
            Assert.True(triangle.CollidesWith(SquarePolygon(), new GeometryHelper.Tolerance(2E-3, 1E-5)));
        }

        [Fact]
        public void APolylineLeavingOneAndAHalfThousandthsFromTheEndOfAnother_DoesNotCollideWithItAtAThousandth()
        {
            // Polyline against polyline (:492).
            var top = new GeoPolyline2(P(0, 100), P(100, 100));
            GeoLine2 side = LeavingTheCorner();
            var leaving = new GeoPolyline2(side.StartPoint, side.EndPoint);

            Assert.False(top.CollidesWith(leaving, Thousandth));
            Assert.False(leaving.CollidesWith(top, Thousandth));
        }

        [Fact]
        public void ASquareAndASegmentWhoseCornersAreFiveTenThousandthsApart_StillCollideAtAThousandth()
        {
            // A guard: the segment leaving (100.0005, 100) at 60 degrees.
            var segment = new GeoLine2(P(100.0005, 100), P(105.0005, 100 + 5 * Math.Sqrt(3)));

            Assert.True(SquarePolygon().CollidesWith(segment, Thousandth));
            Assert.True(SquareRectangle().CollidesWith(segment, Thousandth));
            Assert.False(SquarePolygon().CollidesWith(segment, TenThousandth));
        }

        #endregion
    }
}
