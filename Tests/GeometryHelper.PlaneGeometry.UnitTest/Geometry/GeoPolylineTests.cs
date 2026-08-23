using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Geometry
{
    public class GeoPolylineTests
    {
        [Fact]
        public void Constructor_CalculatesVerticesAndLength()
        {
            var vertices = new List<GeoPoint2>
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            };

            var polyline = new GeoPolyline2(vertices);

            Assert.Equal(3, polyline.VertexCount);
            Assert.Equal(2, polyline.EdgeCount);
            Assert.Equal(20.0, polyline.Length, 4);
        }

        [Fact]
        public void Polyline_NeverClosesItself()
        {
            var vertices = new List<GeoPoint2>
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(0, 10)
            };

            var polyline = new GeoPolyline2(vertices);

            // Four vertices make three edges, not four: the chain stops at the last vertex and never
            // runs back to the first. Enclosing that area is what GeoPolygon2 is for.
            Assert.Equal(4, polyline.VertexCount);
            Assert.Equal(3, polyline.EdgeCount);
            Assert.Equal(30.0, polyline.Length, 4);

            GeoPolygon2 closed = polyline.ToPolygon();
            Assert.Equal(4, closed.EdgeCount);
            Assert.Equal(40.0, closed.Length, 4);
        }

        [Fact]
        public void DistanceAndLocate_CalculatesCorrectly()
        {
            var polyline = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            });

            Assert.Equal(PointLocation.OnSide, polyline.Locate(new GeoPoint2(5, 0)));
            Assert.Equal(PointLocation.OnSide, polyline.Locate(new GeoPoint2(10, 5)));
            Assert.Equal(PointLocation.OutSide, polyline.Locate(new GeoPoint2(5, 5)));

            Assert.Equal(5.0, polyline.DistanceTo(new GeoPoint2(5, 5)), 4);
        }

        [Fact]
        public void GetIntersections_FindsSegmentIntersections()
        {
            var polyline = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            });

            var line = new GeoLine2(new GeoPoint2(5, -5), new GeoPoint2(5, 5));
            var pts = polyline.GetIntersections(line);

            Assert.Single(pts);
            Assert.True(pts[0].IsEqualTo(new GeoPoint2(5, 0)));
        }
        [Fact]
        public void Polyline_TranslateAndRotate_PreserveShape()
        {
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            var moved = polyline.Translate(new GeoVector2(5, -5));
            Assert.True(moved[0].IsEqualTo(new GeoPoint2(5, -5)));
            Assert.Equal(polyline.Length, moved.Length, 9);
            Assert.Equal(moved, polyline + new GeoVector2(5, -5));
            Assert.Equal(polyline, moved - new GeoVector2(5, -5));

            var rotated = polyline.RotateBy(Math.PI / 2.0, new GeoPoint2(0, 0));
            Assert.True(rotated[1].IsEqualTo(new GeoPoint2(0, 10)));
            Assert.Equal(polyline.Length, rotated.Length, 9);
        }

        [Fact]
        public void ToPolygon_ClosesTheChain()
        {
            // The migration path for geometry that used to be a closed polyline.
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var polygon = polyline.ToPolygon();

            Assert.Equal(3, polygon.VertexCount);
            Assert.Equal(3, polygon.EdgeCount);

            // Two vertices cannot enclose anything.
            var degenerate = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            Assert.Throws<ArgumentException>(() => degenerate.ToPolygon());
        }

        [Fact]
        public void EdgeCount_IsAlwaysOneLessThanVertexCount()
        {
            // Every route into a GeoPolyline2 upholds the two-vertex minimum, which is what lets
            // EdgeCount subtract one without guarding the result.
            Assert.Equal(1, new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 1)).EdgeCount);
            Assert.Equal(2, new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 1), new GeoPoint2(2, 0)).EdgeCount);

            Assert.Throws<ArgumentException>(() => new GeoPolyline2(new GeoPoint2(0, 0)));
            Assert.Throws<ArgumentException>(
                () => new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(0, 0)));

            // Splitting and cloning go through the trusted constructor, and never produce a shorter one.
            var path = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            Assert.Equal(path.EdgeCount, path.Clone().EdgeCount);

            foreach (GeoPolyline2 piece in Splition2.SplitAtDistances(path, new[] { 5.0, 10.0, 15.0 }))
            {
                Assert.True(piece.VertexCount >= 2);
                Assert.Equal(piece.VertexCount - 1, piece.EdgeCount);
            }
        }

        [Fact]
        public void Polyline_GetClosestOnBoundary_WorksCorrectly()
        {
            var polyline1 = new GeoPolyline2(new GeoPoint2(0.0, 0.0), new GeoPoint2(10.0, 0.0));
            var polyline2 = new GeoPolyline2(new GeoPoint2(0.0, 5.0), new GeoPoint2(10.0, 5.0));

            // Test 1: Polyline - Polyline
            var segPolylines = polyline1.GetClosestOnBoundary(polyline2);
            Assert.Equal(5.0, segPolylines.Length, 9);
            Assert.Equal(0.0, segPolylines.StartPoint.Y, 9);
            Assert.Equal(5.0, segPolylines.EndPoint.Y, 9);

            // Test 2: Polyline - Circle
            var circle = new GeoCircle2(new GeoPoint2(5.0, 5.0), 2.0);
            var segCircle = polyline1.GetClosestOnBoundary(circle);
            Assert.Equal(3.0, segCircle.Length, 9);
            Assert.True(segCircle.StartPoint.IsEqualTo(new GeoPoint2(5.0, 0.0)));
            Assert.True(segCircle.EndPoint.IsEqualTo(new GeoPoint2(5.0, 3.0)));
        }
    }
}

