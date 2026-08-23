using System;
using System.Linq;
using CommonGeometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    public class SplitionPolylineMultipleTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void Polyline_SplitByMultiplePoints_SplitsCorrectly()
        {
            // Subject runs: (0, 0) -> (5, 0) -> (5, 5)
            var subject = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5));
            var points = new[]
            {
                new GeoPoint2(2, 0),
                new GeoPoint2(5, 3),
                new GeoPoint2(5, 6) // Outside
            };

            Assert.True(subject.TrySplitBy(points, out GeoPolyline2[] pieces));
            Assert.Equal(3, pieces.Length);

            // Piece 0: (0,0) -> (2,0)
            Assert.Equal(2, pieces[0].VertexCount);
            Assert.True(pieces[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0][1].IsEqualTo(new GeoPoint2(2, 0), Tol));

            // Piece 1: (2,0) -> (5,0) -> (5,3)
            Assert.Equal(3, pieces[1].VertexCount);
            Assert.True(pieces[1][0].IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(pieces[1][1].IsEqualTo(new GeoPoint2(5, 0), Tol));
            Assert.True(pieces[1][2].IsEqualTo(new GeoPoint2(5, 3), Tol));

            // Piece 2: (5,3) -> (5,5)
            Assert.Equal(2, pieces[2].VertexCount);
            Assert.True(pieces[2][0].IsEqualTo(new GeoPoint2(5, 3), Tol));
            Assert.True(pieces[2][1].IsEqualTo(new GeoPoint2(5, 5), Tol));

            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitByMultipleLines_SplitsCorrectly()
        {
            var subject = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5));
            var cutters = new[]
            {
                new GeoLine2(new GeoPoint2(2, -1), new GeoPoint2(2, 1)),
                new GeoLine2(new GeoPoint2(4, 3), new GeoPoint2(6, 3))
            };

            Assert.True(subject.TrySplitBy(cutters, out GeoPolyline2[] pieces));
            Assert.Equal(3, pieces.Length);

            Assert.True(pieces[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0][pieces[0].VertexCount - 1].IsEqualTo(new GeoPoint2(2, 0), Tol));

            Assert.True(pieces[1][0].IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(pieces[1][pieces[1].VertexCount - 1].IsEqualTo(new GeoPoint2(5, 3), Tol));

            Assert.True(pieces[2][0].IsEqualTo(new GeoPoint2(5, 3), Tol));
            Assert.True(pieces[2][pieces[2].VertexCount - 1].IsEqualTo(new GeoPoint2(5, 5), Tol));

            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitByMultiplePolylines_SplitsCorrectly()
        {
            var subject = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var cutters = new[]
            {
                new GeoPolyline2(new GeoPoint2(3, -2), new GeoPoint2(3, 2)),
                new GeoPolyline2(new GeoPoint2(7, -2), new GeoPoint2(7, 2))
            };

            Assert.True(subject.TrySplitBy(cutters, out GeoPolyline2[] pieces));
            Assert.Equal(3, pieces.Length);

            Assert.True(pieces[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pieces[0][1].IsEqualTo(new GeoPoint2(3, 0), Tol));

            Assert.True(pieces[1][0].IsEqualTo(new GeoPoint2(3, 0), Tol));
            Assert.True(pieces[1][1].IsEqualTo(new GeoPoint2(7, 0), Tol));

            Assert.True(pieces[2][0].IsEqualTo(new GeoPoint2(7, 0), Tol));
            Assert.True(pieces[2][1].IsEqualTo(new GeoPoint2(10, 0), Tol));

            Assert.Equal(subject.Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitByMultiplePolygons_SplitsCorrectly()
        {
            var subject = new GeoPolyline2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));
            var poly1 = new GeoPolygon2(new GeoPoint2(0, -1), new GeoPoint2(2, -1), new GeoPoint2(2, 1), new GeoPoint2(0, 1));
            var poly2 = new GeoPolygon2(new GeoPoint2(8, -1), new GeoPoint2(10, -1), new GeoPoint2(10, 1), new GeoPoint2(8, 1));

            Assert.True(subject.TrySplitBy(new[] { poly1, poly2 }, out GeoPolyline2[] inside, out GeoPolyline2[] outside));
            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);

            Assert.True(outside[0][0].IsEqualTo(new GeoPoint2(-5, 0), Tol));
            Assert.True(outside[0][1].IsEqualTo(new GeoPoint2(0, 0), Tol));

            Assert.True(inside[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(inside[0][1].IsEqualTo(new GeoPoint2(2, 0), Tol));

            Assert.True(outside[1][0].IsEqualTo(new GeoPoint2(2, 0), Tol));
            Assert.True(outside[1][1].IsEqualTo(new GeoPoint2(8, 0), Tol));

            Assert.True(inside[1][0].IsEqualTo(new GeoPoint2(8, 0), Tol));
            Assert.True(inside[1][1].IsEqualTo(new GeoPoint2(10, 0), Tol));

            Assert.True(outside[2][0].IsEqualTo(new GeoPoint2(10, 0), Tol));
            Assert.True(outside[2][1].IsEqualTo(new GeoPoint2(15, 0), Tol));

            Assert.Equal(subject.Length, inside.Sum(p => p.Length) + outside.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitByOverlappingPolygons_MergesCorrectly()
        {
            var subject = new GeoPolyline2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));
            
            // Two 2x2 squares sharing a boundary at X = 2
            var poly1 = new GeoPolygon2(new GeoPoint2(0, -1), new GeoPoint2(2, -1), new GeoPoint2(2, 1), new GeoPoint2(0, 1));
            var poly2 = new GeoPolygon2(new GeoPoint2(2, -1), new GeoPoint2(4, -1), new GeoPoint2(4, 1), new GeoPoint2(2, 1));

            Assert.True(subject.TrySplitBy(new[] { poly1, poly2 }, out GeoPolyline2[] inside, out GeoPolyline2[] outside));
            
            // Do kề sát nhau, phần inside từ 0 đến 4 được gộp thành 1 polyline duy nhất
            Assert.Single(inside);
            Assert.True(inside[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(inside[0][1].IsEqualTo(new GeoPoint2(4, 0), Tol));

            // Phần outside gồm [-5, 0] và [4, 15]
            Assert.Equal(2, outside.Length);
            Assert.True(outside[0][0].IsEqualTo(new GeoPoint2(-5, 0), Tol));
            Assert.True(outside[0][1].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(outside[1][0].IsEqualTo(new GeoPoint2(4, 0), Tol));
            Assert.True(outside[1][1].IsEqualTo(new GeoPoint2(15, 0), Tol));
        }

        [Fact]
        public void Polyline_SplitByNullArguments_ThrowsException()
        {
            var subject = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPoint2[])null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoLine2[])null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPolyline2[])null, out _));
            Assert.Throws<ArgumentNullException>(() => subject.TrySplitBy((GeoPolygon2[])null, out _, out _));
        }
    }
}
