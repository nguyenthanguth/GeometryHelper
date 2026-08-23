using System;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Geometry
{
    public class GeoCircleTests
    {
        [Fact]
        public void Constructor_SetsCenterAndRadiusCorrectly()
        {
            var center = new GeoPoint2(10, 20);
            var circle = new GeoCircle2(center, 15);

            Assert.Equal(10, circle.Center.X);
            Assert.Equal(20, circle.Center.Y);
            Assert.Equal(15, circle.Radius);
            Assert.Equal(30, circle.Diameter);
            Assert.True(circle.Area > 0);
            Assert.True(circle.Circumference > 0);
        }

        [Fact]
        public void ContainsAndLocate_IdentifiesInsideOutsideOnSide()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);

            Assert.True(circle.Contains(new GeoPoint2(0, 0)));
            Assert.True(circle.Contains(new GeoPoint2(5, 5)));
            Assert.Equal(PointLocation.Inside, circle.Locate(new GeoPoint2(3, 4)));

            Assert.Equal(PointLocation.OnSide, circle.Locate(new GeoPoint2(10, 0)));
            Assert.Equal(PointLocation.OnSide, circle.Locate(new GeoPoint2(0, 10)));

            Assert.False(circle.Contains(new GeoPoint2(10.1, 0)));
            Assert.Equal(PointLocation.OutSide, circle.Locate(new GeoPoint2(15, 15)));
        }

        [Fact]
        public void CollidesWith_DetectsLineAndCircleCollisions()
        {
            var c1 = new GeoCircle2(new GeoPoint2(0, 0), 10);
            var c2 = new GeoCircle2(new GeoPoint2(15, 0), 10);
            var c3 = new GeoCircle2(new GeoPoint2(30, 0), 5);

            Assert.True(c1.CollidesWith(c2));
            Assert.False(c1.CollidesWith(c3));

            var line1 = new GeoLine2(new GeoPoint2(-20, 0), new GeoPoint2(20, 0));
            var line2 = new GeoLine2(new GeoPoint2(0, 20), new GeoPoint2(10, 20));

            Assert.True(c1.CollidesWith(line1));
            Assert.False(c1.CollidesWith(line2));
        }

        [Fact]
        public void GetIntersections_CalculatesAccurateIntersectionPoints()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 5);
            var line = new GeoLine2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0));

            var pts = circle.GetIntersections(line);
            Assert.Equal(2, pts.Length);
            Assert.True(pts[0].IsEqualTo(new GeoPoint2(-5, 0)) || pts[0].IsEqualTo(new GeoPoint2(5, 0)));
            Assert.True(pts[1].IsEqualTo(new GeoPoint2(-5, 0)) || pts[1].IsEqualTo(new GeoPoint2(5, 0)));
        }

        [Fact]
        public void Circle_ToRectangle_ConvertsToOrientedBoundingSquare()
        {
            var center = new GeoPoint2(10.0, 20.0);
            var circle = new GeoCircle2(center, 5.0);
            double angleRad = 0.5;

            GeoRectangle2 rect = circle.ToRectangle(angleRad);

            Assert.True(rect.Center.IsEqualTo(center));
            Assert.Equal(10.0, rect.Width, 9);
            Assert.Equal(10.0, rect.Height, 9);
            Assert.Equal(angleRad, rect.AngleRad, 9);

            // Verify corners are at distance of R * sqrt(2) from center
            GeoPoint2[] vertices = rect.GetVertices();
            double expectedDist = 5.0 * Math.Sqrt(2.0);
            foreach (var vertex in vertices)
            {
                Assert.Equal(expectedDist, rect.Center.DistanceTo(vertex), 9);
            }
        }

        [Fact]
        public void Circle_GetClosestOnBoundary_WorksCorrectly()
        {
            var circle1 = new GeoCircle2(new GeoPoint2(0.0, 0.0), 3.0);
            var circle2 = new GeoCircle2(new GeoPoint2(10.0, 0.0), 2.0);

            // Test 1: Circle - Circle (disjoint)
            // Distance2 between centers is 10. Closest on c1 is (3, 0), on c2 is (8, 0), length = 5
            var segCircles = circle1.GetClosestOnBoundary(circle2);
            Assert.Equal(5.0, segCircles.Length, 9);
            Assert.True(segCircles.StartPoint.IsEqualTo(new GeoPoint2(3.0, 0.0)));
            Assert.True(segCircles.EndPoint.IsEqualTo(new GeoPoint2(8.0, 0.0)));

            // Test 2: Circle - Line
            var line = new GeoLine2(10.0, -5.0, 10.0, 5.0);
            var segLine = circle1.GetClosestOnBoundary(line);
            Assert.Equal(7.0, segLine.Length, 9);
            Assert.True(segLine.StartPoint.IsEqualTo(new GeoPoint2(3.0, 0.0)));
            Assert.True(segLine.EndPoint.IsEqualTo(new GeoPoint2(10.0, 0.0)));

            // Test 3: Circle - Rectangle
            var rect = new GeoRectangle2(new GeoPoint2(10.0, 0.0), 4.0, 4.0, 0.0); // left edge at X = 8
            var segRect = circle1.GetClosestOnBoundary(rect);
            Assert.Equal(5.0, segRect.Length, 9);
            Assert.True(segRect.StartPoint.IsEqualTo(new GeoPoint2(3.0, 0.0)));
            Assert.True(segRect.EndPoint.IsEqualTo(new GeoPoint2(8.0, 0.0)));
        }
    }
}
