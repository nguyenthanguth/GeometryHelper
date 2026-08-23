using System;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    public class SplitionLineMultipleTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void Line_SplitByMultipleLines_SplitsCorrectly()
        {
            // Subject runs along X-axis from (0, 0) to (10, 0)
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            // Cutters intersect at X = 3.0 and X = 7.0
            var cutters = new[]
            {
                new GeoLine2(new GeoPoint2(3, -5), new GeoPoint2(3, 5)),
                new GeoLine2(new GeoPoint2(7, -5), new GeoPoint2(7, 5))
            };

            Assert.True(subject.TrySplitBy(cutters, out GeoLine2[] pieces));
            Assert.Equal(3, pieces.Length);

            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(3, 0), Tol));

            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint2(3, 0), Tol));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(7, 0), Tol));

            Assert.True(pieces[2].StartPoint.IsEqualTo(new GeoPoint2(7, 0), Tol));
            Assert.True(pieces[2].EndPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));

            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Line_SplitByMultipleLines_NoIntersections_ReturnsFalse()
        {
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            // Cutters are parallel or far away
            var cutters = new[]
            {
                new GeoLine2(new GeoPoint2(-5, -5), new GeoPoint2(-5, 5)),
                new GeoLine2(new GeoPoint2(15, -5), new GeoPoint2(15, 5))
            };

            Assert.False(subject.TrySplitBy(cutters, out GeoLine2[] pieces));
            Assert.Single(pieces);
            Assert.True(pieces[0].StartPoint.IsEqualTo(subject.StartPoint, Tol));
            Assert.True(pieces[0].EndPoint.IsEqualTo(subject.EndPoint, Tol));
        }

        [Fact]
        public void Line_SplitByPolyline_SplitsCorrectly()
        {
            // Subject runs along X-axis from (0, 0) to (10, 0)
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            // A staple shape whose two uprights cross the X-axis at X = 2.0 and X = 8.0. The earlier
            // chevron shape crossed at 20/7 and 50/7 instead, which is what the expectations below
            // were actually being compared against.
            var cutter = new GeoPolyline2(
                new GeoPoint2(2, -2),
                new GeoPoint2(2, 2),
                new GeoPoint2(8, 2),
                new GeoPoint2(8, -2)
            );

            Assert.True(subject.TrySplitBy(cutter, out GeoLine2[] pieces));
            Assert.Equal(3, pieces.Length);

            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(2, 0), Tol));

            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(8, 0), Tol));

            Assert.True(pieces[2].StartPoint.IsEqualTo(new GeoPoint2(8, 0), Tol));
            Assert.True(pieces[2].EndPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));

            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Line_SplitByMultiplePoints_SplitsCorrectly()
        {
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var points = new[]
            {
                new GeoPoint2(4, 0),
                new GeoPoint2(6, 0),
                new GeoPoint2(12, 0) // Outside, should be ignored
            };

            Assert.True(subject.TrySplitBy(points, out GeoLine2[] pieces));
            Assert.Equal(3, pieces.Length);
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(4, 0), Tol));
            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint2(4, 0), Tol));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(6, 0), Tol));
            Assert.True(pieces[2].StartPoint.IsEqualTo(new GeoPoint2(6, 0), Tol));
            Assert.True(pieces[2].EndPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));
            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Line_SplitByMultiplePolygons_SplitsCorrectly()
        {
            var subject = new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));
            
            // Two 2x2 squares along the X-axis
            var poly1 = new GeoPolygon2(new GeoPoint2(0, -1), new GeoPoint2(2, -1), new GeoPoint2(2, 1), new GeoPoint2(0, 1));
            var poly2 = new GeoPolygon2(new GeoPoint2(8, -1), new GeoPoint2(10, -1), new GeoPoint2(10, 1), new GeoPoint2(8, 1));
            
            Assert.True(subject.TrySplitBy(new[] { poly1, poly2 }, out GeoLine2[] inside, out GeoLine2[] outside));
            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);
            
            Assert.True(outside[0].StartPoint.IsEqualTo(new GeoPoint2(-5, 0), Tol));
            Assert.True(outside[0].EndPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(inside[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(inside[0].EndPoint.IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(outside[1].StartPoint.IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(outside[1].EndPoint.IsEqualTo(new GeoPoint2(8, 0), Tol));
            Assert.True(inside[1].StartPoint.IsEqualTo(new GeoPoint2(8, 0), Tol));
            Assert.True(inside[1].EndPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));
            Assert.True(outside[2].StartPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));
            Assert.True(outside[2].EndPoint.IsEqualTo(new GeoPoint2(15, 0), Tol));
            
            Assert.Equal(subject.Length, inside.Sum(p => p.Length) + outside.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Line_SplitByOverlappingPolygons_MergesCorrectly()
        {
            var subject = new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));
            
            // Two 2x2 squares sharing a boundary at X = 2
            var poly1 = new GeoPolygon2(new GeoPoint2(0, -1), new GeoPoint2(2, -1), new GeoPoint2(2, 1), new GeoPoint2(0, 1));
            var poly2 = new GeoPolygon2(new GeoPoint2(2, -1), new GeoPoint2(4, -1), new GeoPoint2(4, 1), new GeoPoint2(2, 1));
            
            Assert.True(subject.TrySplitBy(new[] { poly1, poly2 }, out GeoLine2[] inside, out GeoLine2[] outside));
            
            // Do kề sát nhau, phần inside từ 0 đến 4 được gộp thành 1 đoạn duy nhất
            Assert.Single(inside);
            Assert.True(inside[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(inside[0].EndPoint.IsEqualTo(new GeoPoint2(4, 0), Tol));
            
            // Phần outside gồm [-5, 0] và [4, 15]
            Assert.Equal(2, outside.Length);
            Assert.True(outside[0].StartPoint.IsEqualTo(new GeoPoint2(-5, 0), Tol));
            Assert.True(outside[0].EndPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(outside[1].StartPoint.IsEqualTo(new GeoPoint2(4, 0), Tol));
            Assert.True(outside[1].EndPoint.IsEqualTo(new GeoPoint2(15, 0), Tol));
        }

        [Fact]
        public void Line_SplitByMultiplePolylines_SplitsCorrectly()
        {
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            
            var polyline1 = new GeoPolyline2(new GeoPoint2(3, -2), new GeoPoint2(3, 2));
            var polyline2 = new GeoPolyline2(new GeoPoint2(7, -2), new GeoPoint2(7, 2));

            Assert.True(subject.TrySplitBy(new[] { polyline1, polyline2 }, out GeoLine2[] pieces));
            Assert.Equal(3, pieces.Length);
            
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(3, 0), Tol));
            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint2(3, 0), Tol));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(7, 0), Tol));
            Assert.True(pieces[2].StartPoint.IsEqualTo(new GeoPoint2(7, 0), Tol));
            Assert.True(pieces[2].EndPoint.IsEqualTo(new GeoPoint2(10, 0), Tol));
            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Line_SplitByNullArguments_ThrowsException()
        {
            var subject = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoLine2[])null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPolyline2)null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPoint2[])null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPolygon2[])null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPolyline2[])null, out _));
        }
    }
}
