using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Geometry
{
    public class GeoPointTests
    {
        [Fact]
        public void Point_BasicOperations_WorkCorrectly()
        {
            var p1 = new GeoPoint2(1.0, 2.0);
            var v = new GeoVector2(3.0, 4.0);

            var p2 = p1 + v; // (4.0, 6.0)
            Assert.Equal(new GeoPoint2(4.0, 6.0), p2);

            var p3 = p2 - v; // (1.0, 2.0)
            Assert.Equal(p1, p3);

            var vOffset = p2 - p1;
            Assert.Equal(v, vOffset);

            Assert.Equal(5.0, p1.DistanceTo(p2), 12);
            Assert.Equal(25.0, p1.GetDistanceSquaredTo(p2), 12);
        }

        [Fact]
        public void Point_RotationAndScaling_WorkCorrectly()
        {
            var origin = new GeoPoint2(0.0, 0.0);
            var p = new GeoPoint2(1.0, 0.0);

            // Rotate 90 degrees counter-clockwise
            var rotated = p.RotateBy(Math.PI / 2.0, origin);
            Assert.True(rotated.IsEqualTo(new GeoPoint2(0.0, 1.0)));

            // Scale by factor of 2.5
            var scaled = p.ScaleBy(2.5, origin);
            Assert.True(scaled.IsEqualTo(new GeoPoint2(2.5, 0.0)));
        }

        [Fact]
        public void Point_LocateIn_WorksCorrectlyForShapes()
        {
            var ptInside = new GeoPoint2(5, 5);
            var ptOnSide = new GeoPoint2(10, 5);
            var ptOutSide = new GeoPoint2(15, 5);

            var rect = new GeoRectangle2(new GeoPoint2(5, 5), 10, 10, 0);
            var poly = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(0, 10)
            });

            // Rectangle
            Assert.Equal(PointLocation.Inside, ptInside.LocateIn(rect));
            Assert.Equal(PointLocation.OnSide, ptOnSide.LocateIn(rect));
            Assert.Equal(PointLocation.OutSide, ptOutSide.LocateIn(rect));

            // Polygon
            Assert.Equal(PointLocation.Inside, ptInside.LocateIn(poly));
            Assert.Equal(PointLocation.OnSide, ptOnSide.LocateIn(poly));
            Assert.Equal(PointLocation.OutSide, ptOutSide.LocateIn(poly));
        }

        [Fact]
        public void Point_ProjectTo_WorksCorrectlyForShapes()
        {
            var pt = new GeoPoint2(5, 10);
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 5);

            var projLine = pt.GetClosestPointOnBoundary(line);
            Assert.True(projLine.IsEqualTo(new GeoPoint2(5, 0)));

            var projCircle = new GeoPoint2(10, 0).GetClosestPointOnBoundary(circle);
            Assert.True(projCircle.IsEqualTo(new GeoPoint2(5, 0)));
        }

        [Fact]
        public void Point_DistanceTo_WorksCorrectlyForShapes()
        {
            var pt = new GeoPoint2(0, 10);
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 4);
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 10, 6, 0);

            Assert.Equal(6.0, pt.DistanceTo(circle), 4);
            Assert.Equal(7.0, pt.DistanceTo(rect), 4);
        }

        [Fact]
        public void IsPointOn_ReachesEveryShapeThatHasABoundaryTest()
        {
            var point = new GeoPoint2(5, 0);

            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 5);

            // Both directions of the facade route to the same Containment2 call.
            Assert.True(point.IsPointOn(line));
            Assert.Equal(line.IsPointOn(point), point.IsPointOn(line));
            Assert.True(point.IsPointOn(polyline));
            Assert.True(point.IsPointOn(circle));

            var off = new GeoPoint2(5, 3);
            Assert.False(off.IsPointOn(line));
            Assert.False(off.IsPointOn(line, new Tolerance(1E-4, 1E-4)));
            Assert.True(off.IsPointOn(line, new Tolerance(5.0, 5.0)));
        }

    }
}
