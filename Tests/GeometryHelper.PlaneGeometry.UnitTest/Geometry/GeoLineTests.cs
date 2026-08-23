using System;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest
{
    public class LineTests
    {
        [Fact]
        public void Line_CreationAndProperties_WorkCorrectly()
        {
            var p1 = new GeoPoint2(1.0, 1.0);
            var p2 = new GeoPoint2(4.0, 5.0);
            var GeoLine2 = new GeoLine2(p1, p2);

            Assert.Equal(p1, GeoLine2.StartPoint);
            Assert.Equal(p2, GeoLine2.EndPoint);
            Assert.Equal(5.0, GeoLine2.Length, 12);
            Assert.Equal(25.0, GeoLine2.LengthSquared, 12);
            Assert.Equal(new GeoPoint2(2.5, 3.0), GeoLine2.MidPoint);
            Assert.Equal(new GeoVector2(3.0, 4.0), GeoLine2.Direction);
        }

        [Fact]
        public void Line_ProjectionAndClosestPoint_WorkCorrectly()
        {
            var GeoLine2 = new GeoLine2(0.0, 0.0, 10.0, 0.0); // Horizontal on the X-axis

            // Point on the line segment
            Assert.Equal(0.5, GeoLine2.GetParameterAtPoint(new GeoPoint2(5.0, 0.0)), 12);
            Assert.Equal(new GeoPoint2(5.0, 0.0), GeoLine2.GetClosestPointOnBoundary(new GeoPoint2(5.0, 5.0)));
            Assert.Equal(5.0, GeoLine2.DistanceTo(new GeoPoint2(5.0, 5.0)), 12);

            // Point outside the endpoints
            Assert.Equal(-0.2, GeoLine2.GetParameterAtPoint(new GeoPoint2(-2.0, 0.0)), 12);
            Assert.Equal(GeoLine2.StartPoint, GeoLine2.GetClosestPointOnBoundary(new GeoPoint2(-2.0, 5.0)));

            Assert.True(GeoLine2.IsPointOn(new GeoPoint2(3.0, 0.0)));
            Assert.False(GeoLine2.IsPointOn(new GeoPoint2(3.0, 0.1)));
        }

        [Fact]
        public void Line_Intersection_WorksCorrectly()
        {
            var l1 = new GeoLine2(0.0, 0.0, 10.0, 10.0);
            var l2 = new GeoLine2(0.0, 10.0, 10.0, 0.0);

            // Intersect at the center (5, 5)
            Assert.True(l1.TryIntersectWith(l2, out var hit));
            Assert.True(hit.IsEqualTo(new GeoPoint2(5.0, 5.0)));

            // Song song
            var l3 = new GeoLine2(0.0, 2.0, 10.0, 12.0);
            Assert.False(l1.TryIntersectWith(l3, out _));

            // Do not intersect directly (only intersect on extended line)
            var l4 = new GeoLine2(20.0, 10.0, 30.0, 0.0);
            Assert.False(l1.TryIntersectWith(l4, out _));
        }

        [Fact]
        public void Line_Direction_ReturnsZeroForDegenerateLine()
        {
            var GeoLine2 = new GeoLine2(1.0, 1.0, 1.0, 1.0); // Start point equals end point
            Assert.Equal(GeoVector2.Zero, GeoLine2.Direction);
            Assert.Equal(0.0, GeoLine2.Length);
            Assert.False(GeoLine2.Direction.TryGetNormal(out _));
        }

        [Fact]
        public void Line_CollinearAndOverlappingIntersection_WorkCorrectly()
        {
            // Two line segments lying on the same line
            var l1 = new GeoLine2(0.0, 0.0, 5.0, 0.0);

            // Case 1: Lying on the extended line but not touching
            var l2 = new GeoLine2(10.0, 0.0, 15.0, 0.0);
            Assert.False(l1.TryIntersectWith(l2, out _));

            // Case 2: Collinear Overlap
            var l3 = new GeoLine2(3.0, 0.0, 8.0, 0.0);
            // Basic geometric algorithms in AutoCAD/libraries typically return false for collinear overlap
            // because there are infinite intersection points; we test the library's actual behavior:
            Assert.False(l1.TryIntersectWith(l3, out _));
        }

        [Fact]
        public void Line_PointProjectionEdgeCases_WorkCorrectly()
        {
            var GeoLine2 = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            // Projection2 falls exactly on the start point (parameter t = 0)
            Assert.Equal(0.0, GeoLine2.GetParameterAtPoint(new GeoPoint2(0.0, 5.0)), 12);
            Assert.Equal(GeoLine2.StartPoint, GeoLine2.GetClosestPointOnBoundary(new GeoPoint2(0.0, 5.0)));

            // Projection2 falls exactly on the end point (parameter t = 1)
            Assert.Equal(1.0, GeoLine2.GetParameterAtPoint(new GeoPoint2(10.0, -5.0)), 12);
            Assert.Equal(GeoLine2.EndPoint, GeoLine2.GetClosestPointOnBoundary(new GeoPoint2(10.0, -5.0)));
        }

        [Fact]
        public void Line_TJunctionIntersection_WorksCorrectly()
        {
            var l1 = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            // A perpendicular segment touching exactly at the endpoint
            var l2 = new GeoLine2(5.0, 0.0, 5.0, 5.0);
            Assert.True(l1.TryIntersectWith(l2, out var hit));
            Assert.True(hit.IsEqualTo(new GeoPoint2(5.0, 0.0)));
        }

        [Fact]
        public void Line_GetPointAtParameter_InterpolatesAndExtrapolates()
        {
            var line = new GeoLine2(0.0, 0.0, 10.0, 20.0);

            Assert.True(line.GetPointAtParameter(0.0).IsEqualTo(line.StartPoint));
            Assert.True(line.GetPointAtParameter(1.0).IsEqualTo(line.EndPoint));
            Assert.True(line.GetPointAtParameter(0.5).IsEqualTo(line.MidPoint));

            // Parameter outside [0, 1] for points on the extended line.
            Assert.True(line.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint2(20.0, 40.0)));
            Assert.True(line.GetPointAtParameter(-0.5).IsEqualTo(new GeoPoint2(-5.0, -10.0)));
        }

        [Fact]
        public void Line_GetParameterOf_ReturnsZeroForDegenerateLine()
        {
            // A degenerate segment has no direction so parameter is undefined; must return 0 instead of dividing by 0.
            var degenerate = new GeoLine2(3.0, 3.0, 3.0, 3.0);

            Assert.Equal(0.0, degenerate.GetParameterAtPoint(new GeoPoint2(10.0, 10.0)));
            Assert.Equal(degenerate.StartPoint, degenerate.GetClosestPointOnBoundary(new GeoPoint2(10.0, 10.0)));
        }

        [Fact]
        public void Line_Equality_AndHashCode_WorkCorrectly()
        {
            var a = new GeoLine2(1.0, 2.0, 3.0, 4.0);
            var b = new GeoLine2(1.0, 2.0, 3.0, 4.0);
            var reversed = new GeoLine2(3.0, 4.0, 1.0, 2.0);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals((object)b));

            object notALine = "not a GeoLine2";
            Assert.False(a.Equals(notALine));

            // Reversing direction creates a DIFFERENT line segment: direction is part of its identity.
            Assert.True(a != reversed);
        }

        [Fact]
        public void Line_IntersectsWith_MatchesTryIntersectWith()
        {
            var line = new GeoLine2(0.0, 0.0, 10.0, 10.0);
            var crossing = new GeoLine2(0.0, 10.0, 10.0, 0.0);
            var parallel = new GeoLine2(0.0, 2.0, 10.0, 12.0);

            Assert.Equal(line.TryIntersectWith(crossing, out _), line.CollidesWith(crossing));
            Assert.Equal(line.TryIntersectWith(parallel, out _), line.CollidesWith(parallel));

            Assert.True(line.CollidesWith(crossing));
            Assert.False(line.CollidesWith(parallel));
        }

        [Fact]
        public void Line_IntersectsWith_NullPolygon_Throws()
        {
            var line = new GeoLine2(0.0, 0.0, 10.0, 10.0);

            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoPolygon2)null));
        }

        [Fact]
        public void Line_DistanceTo_MeasuresFromNearestEndWhenProjectionFallsOutside()
        {
            var line = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            // Projection2 falls inside the segment: perpendicular distance.
            Assert.Equal(3.0, line.DistanceTo(new GeoPoint2(4.0, 3.0)), 9);

            // Projection2 falls outside the endpoints: measure directly to that endpoint.
            Assert.Equal(5.0, line.DistanceTo(new GeoPoint2(-3.0, 4.0)), 9);
            Assert.Equal(5.0, line.DistanceTo(new GeoPoint2(13.0, 4.0)), 9);
        }

        [Fact]
        public void Line_IntersectsWithLine_WorksCorrectly()
        {
            var l1 = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            Assert.True(l1.CollidesWith(new GeoLine2(5.0, -5.0, 5.0, 5.0)));   // Intersects
            Assert.False(l1.CollidesWith(new GeoLine2(0.0, 5.0, 10.0, 5.0)));  // Parallel2
            Assert.False(l1.CollidesWith(new GeoLine2(20.0, -5.0, 20.0, 5.0))); // Intersects extension, not segment
        }

        [Fact]
        public void Line_IntersectsWithRectangleAndPolygon_WorksCorrectly()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 4.0);
            var poly = new GeoPolygon2(
                new GeoPoint2(-2.0, -2.0),
                new GeoPoint2(2.0, -2.0),
                new GeoPoint2(2.0, 2.0),
                new GeoPoint2(-2.0, 2.0)
            );

            var crossing = new GeoLine2(-5.0, 0.0, 5.0, 0.0);
            var outside = new GeoLine2(10.0, 10.0, 12.0, 12.0);

            Assert.True(crossing.CollidesWith(rect));
            Assert.False(outside.CollidesWith(rect));

            Assert.True(crossing.CollidesWith(poly));
            Assert.False(outside.CollidesWith(poly));

            // Calling in reverse direction must give the same result
            Assert.True(rect.CollidesWith(crossing));
            Assert.True(poly.CollidesWith(crossing));
        }

        [Fact]
        public void Line_GetClosestOnBoundary_Line_WorksCorrectly()
        {
            var l1 = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            // Test 1: Intersecting lines -> length 0 at intersection point (5, 0)
            var lCrossing = new GeoLine2(5.0, -5.0, 5.0, 5.0);
            var closestIntersecting = l1.GetClosestOnBoundary(lCrossing);
            Assert.Equal(0.0, closestIntersecting.Length, 9);
            Assert.True(closestIntersecting.StartPoint.IsEqualTo(new GeoPoint2(5.0, 0.0)));
            Assert.True(closestIntersecting.EndPoint.IsEqualTo(new GeoPoint2(5.0, 0.0)));

            // Test 2: Parallel2 lines
            var lParallel = new GeoLine2(0.0, 4.0, 10.0, 4.0);
            var closestParallel = l1.GetClosestOnBoundary(lParallel);
            Assert.Equal(4.0, closestParallel.Length, 9);
            Assert.Equal(0.0, closestParallel.StartPoint.Y, 9);
            Assert.Equal(4.0, closestParallel.EndPoint.Y, 9);

            // Test 3: Disjoint / offset lines
            var lDisjoint = new GeoLine2(14.0, 3.0, 20.0, 3.0);
            var closestDisjoint = l1.GetClosestOnBoundary(lDisjoint);
            // Closest is between (10, 0) and (14, 3) -> distance is sqrt(4^2 + 3^2) = 5
            Assert.Equal(5.0, closestDisjoint.Length, 9);
            Assert.True(closestDisjoint.StartPoint.IsEqualTo(new GeoPoint2(10.0, 0.0)));
            Assert.True(closestDisjoint.EndPoint.IsEqualTo(new GeoPoint2(14.0, 3.0)));
        }

        [Fact]
        public void Line_GetClosestOnBoundary_Circle_WorksCorrectly()
        {
            var circle = new GeoCircle2(new GeoPoint2(5.0, 0.0), 2.0);

            // Test 1: Line strictly outside circle
            var lOutside = new GeoLine2(0.0, 5.0, 10.0, 5.0);
            var closestOutside = lOutside.GetClosestOnBoundary(circle);
            Assert.Equal(3.0, closestOutside.Length, 9);
            Assert.True(closestOutside.StartPoint.IsEqualTo(new GeoPoint2(5.0, 5.0)));
            Assert.True(closestOutside.EndPoint.IsEqualTo(new GeoPoint2(5.0, 2.0)));

            // Test 2: Line intersecting circle -> length 0
            var lCrossing = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var closestCrossing = lCrossing.GetClosestOnBoundary(circle);
            Assert.Equal(0.0, closestCrossing.Length, 9);

            // Test 3: Line strictly inside circle
            var lInside = new GeoLine2(4.5, 0.0, 5.5, 0.0);
            var closestInside = lInside.GetClosestOnBoundary(circle);
            // Endpoints are at distance 0.5 from center -> distance to circumference = 2.0 - 0.5 = 1.5
            Assert.Equal(1.5, closestInside.Length, 9);
        }

        [Fact]
        public void Line_GetClosestOnBoundary_Rectangle_WorksCorrectly()
        {
            var rect = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 4.0, 2.0, 0.0); // X: [3, 7], Y: [-1, 1]

            // Test 1: Line outside rectangle
            var lOutside = new GeoLine2(0.0, 5.0, 10.0, 5.0);
            var closestOutside = lOutside.GetClosestOnBoundary(rect);
            // Closest point on line is (5, 5), on rectangle top edge is (5, 1) -> length = 4
            Assert.Equal(4.0, closestOutside.Length, 9);
            Assert.True(closestOutside.StartPoint.IsEqualTo(new GeoPoint2(5.0, 5.0)));
            Assert.True(closestOutside.EndPoint.IsEqualTo(new GeoPoint2(5.0, 1.0)));

            // Test 2: Line intersecting rectangle
            var lIntersecting = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var closestIntersecting = lIntersecting.GetClosestOnBoundary(rect);
            Assert.Equal(0.0, closestIntersecting.Length, 9);

            // Test 3: Line strictly inside rectangle
            var lInside = new GeoLine2(4.5, 0.0, 5.5, 0.0);
            var closestInside = lInside.GetClosestOnBoundary(rect);
            // Distance2 from Y=0 to top/bottom edge Y=+/-1 is 1.0
            Assert.Equal(1.0, closestInside.Length, 9);
        }

        [Fact]
        public void Line_GetClosestOnBoundary_Polyline_WorksCorrectly()
        {
            var polyline = new GeoPolyline2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(5.0, 2.0),
                new GeoPoint2(10.0, 0.0)
            );

            var line = new GeoLine2(0.0, 5.0, 10.0, 5.0);
            var closest = line.GetClosestOnBoundary(polyline);

            // Closest point on line is (5, 5), on polyline vertex is (5, 2) -> distance is 3
            Assert.Equal(3.0, closest.Length, 9);
            Assert.True(closest.StartPoint.IsEqualTo(new GeoPoint2(5.0, 5.0)));
            Assert.True(closest.EndPoint.IsEqualTo(new GeoPoint2(5.0, 2.0)));
        }

        [Fact]
        public void Line_GetClosestOnBoundary_Polygon_WorksCorrectly()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(3.0, 0.0),
                new GeoPoint2(7.0, 0.0),
                new GeoPoint2(5.0, 2.0)
            );

            var line = new GeoLine2(0.0, 5.0, 10.0, 5.0);
            var closest = line.GetClosestOnBoundary(poly);

            // Closest point on line is (5, 5), on apex of triangle is (5, 2) -> distance is 3
            Assert.Equal(3.0, closest.Length, 9);
            Assert.True(closest.StartPoint.IsEqualTo(new GeoPoint2(5.0, 5.0)));
            Assert.True(closest.EndPoint.IsEqualTo(new GeoPoint2(5.0, 2.0)));
        }
    }
}

