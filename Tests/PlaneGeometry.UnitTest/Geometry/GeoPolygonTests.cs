using System;
using System.Collections.Generic;
using System.Linq;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest
{
    public class PolygonTests
    {
        [Fact]
        public void Polygon_WindingAndArea_WorkCorrectly()
        {
            // Counter-clockwise triangle (CCW)
            var p1 = new GeoPoint2(0.0, 0.0);
            var p2 = new GeoPoint2(4.0, 0.0);
            var p3 = new GeoPoint2(0.0, 3.0);
            var poly = new GeoPolygon2(p1, p2, p3);

            Assert.Equal(3, poly.VertexCount);
            Assert.Equal(3, poly.EdgeCount);
            Assert.Equal(6.0, poly.GetArea(), 12);
            Assert.Equal(6.0, poly.GetSignedArea(), 12);
            Assert.False(poly.IsClockwise());
            Assert.Equal(12.0, poly.Length, 9);

            // Clockwise triangle (CW)
            var polyCW = new GeoPolygon2(p1, p3, p2);
            Assert.Equal(6.0, polyCW.GetArea(), 12);
            Assert.Equal(-6.0, polyCW.GetSignedArea(), 12);
            Assert.True(polyCW.IsClockwise());
            Assert.Equal(12.0, polyCW.Length, 9);
        }

        [Fact]
        public void Polygon_Centroid_WorksCorrectly()
        {
            var p1 = new GeoPoint2(0.0, 0.0);
            var p2 = new GeoPoint2(6.0, 0.0);
            var p3 = new GeoPoint2(0.0, 6.0);
            var poly = new GeoPolygon2(p1, p2, p3);

            // The centroid of this right triangle is at (2, 2)
            Assert.True(poly.GetCentroid().IsEqualTo(new GeoPoint2(2.0, 2.0)));
        }

        [Fact]
        public void Polygon_ContainsPoint_WorksCorrectly()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            ); // 4x4 square

            Assert.True(poly.Contains(new GeoPoint2(2.0, 2.0)));   // Point inside
            Assert.True(poly.Contains(new GeoPoint2(0.0, 2.0)));   // Point on boundary
            Assert.False(poly.Contains(new GeoPoint2(5.0, 2.0)));  // Point outside
        }

        [Fact]
        public void Polygon_IntersectsWithRectangle_WorksCorrectly()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            ); // 4x4 square

            // 1. Mutually intersecting
            var rect1 = new GeoRectangle2(new GeoPoint2(4.0, 4.0), 2.0, 2.0, Math.PI / 4.0); // Center at square vertex, rotated 45 degrees
            Assert.True(poly.CollidesWith(rect1));

            // 2. Containing the rectangle entirely
            var rect2 = new GeoRectangle2(new GeoPoint2(2.0, 2.0), 1.0, 1.0, 0.1);
            Assert.True(poly.CollidesWith(rect2));

            // 3. Completely separated
            var rect3 = new GeoRectangle2(new GeoPoint2(8.0, 8.0), 2.0, 2.0, 0.5);
            Assert.False(poly.CollidesWith(rect3));
        }

        [Fact]
        public void Polygon_IntersectsWithLine_WorksCorrectly()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            ); // 4x4 square

            // 1. Line segment passing through the square
            Assert.True(poly.CollidesWith(new GeoLine2(-2.0, 2.0, 6.0, 2.0)));

            // 2. Line segment completely inside (does not intersect any edges)
            Assert.True(poly.CollidesWith(new GeoLine2(1.0, 1.0, 3.0, 3.0)));

            // 3. Line segment touching boundary
            Assert.True(poly.CollidesWith(new GeoLine2(-2.0, 4.0, 2.0, 4.0)));

            // 4. Completely separated
            Assert.False(poly.CollidesWith(new GeoLine2(6.0, 6.0, 8.0, 8.0)));
        }

        [Fact]
        public void Polygon_IntersectsWithPolygon_WorksCorrectly()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            ); // 4x4 square

            // 1. Partially overlapping
            var overlapping = new GeoPolygon2(
                new GeoPoint2(2.0, 2.0),
                new GeoPoint2(6.0, 2.0),
                new GeoPoint2(6.0, 6.0),
                new GeoPoint2(2.0, 6.0)
            );
            Assert.True(poly.CollidesWith(overlapping));

            // 2. Nested entirely inside: no edges intersect, must be recognized via containment check
            var inner = new GeoPolygon2(
                new GeoPoint2(1.0, 1.0),
                new GeoPoint2(3.0, 1.0),
                new GeoPoint2(3.0, 3.0),
                new GeoPoint2(1.0, 3.0)
            );
            Assert.True(poly.CollidesWith(inner));

            // 3. Containment2 relation must be symmetric in both calling directions
            Assert.True(inner.CollidesWith(poly));

            // 4. Completely separated
            var far = new GeoPolygon2(
                new GeoPoint2(10.0, 10.0),
                new GeoPoint2(12.0, 10.0),
                new GeoPoint2(12.0, 12.0)
            );
            Assert.False(poly.CollidesWith(far));
        }

        [Fact]
        public void Polygon_Constructor_RejectsDegeneratePolygon()
        {
            // A polygon with only 2 vertices is degenerate. The constructor blocks it immediately instead of creating
            // an object that cannot answer Contains meaningfully, so the correct test is to assert an exception.
            Assert.Throws<ArgumentException>(
                () => new GeoPolygon2(new GeoPoint2(0.0, 0.0), new GeoPoint2(1.0, 1.0)));
        }

        [Fact]
        public void Polygon_CentroidConcavePolygon_WorksCorrectly()
        {
            // L-shaped polygon (concave)
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 2.0),
                new GeoPoint2(2.0, 2.0),
                new GeoPoint2(2.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            );

            // Centroid and area of arbitrary polygon using Shoelace / Green Theorem algorithm
            // Area = 12
            Assert.Equal(12.0, poly.GetArea(), 12);

            // Composed of two rectangles: [0,4]x[0,2] area 8 center (2,1) and [0,2]x[2,4] area 4 center (1,3).
            // Centroid = (8*2 + 4*1)/12 = 5/3 along X, (8*1 + 4*3)/12 = 5/3 along Y.
            Assert.True(poly.GetCentroid().IsEqualTo(new GeoPoint2(5.0 / 3.0, 5.0 / 3.0)));
        }

        [Fact]
        public void Polygon_ContainsPointConcavePolygon_WorksCorrectly()
        {
            // L-shaped polygon (concave)
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 2.0),
                new GeoPoint2(2.0, 2.0),
                new GeoPoint2(2.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            );

            // Point (3, 3) lies outside the polygon, although it is within the polygon's [0, 4] x [0, 4] bounding box
            Assert.False(poly.Contains(new GeoPoint2(3.0, 3.0)));

            // Point (1, 3) lies inside the vertical branch of the L-shape
            Assert.True(poly.Contains(new GeoPoint2(1.0, 3.0)));

            // Point (3, 1) lies inside the horizontal branch of the L-shape
            Assert.True(poly.Contains(new GeoPoint2(3.0, 1.0)));
        }

        [Fact]
        public void Polygon_ZeroAreaCollinear_ReturnsCorrectArea()
        {
            // 3 collinear points -> flat polygon area = 0
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(5.0, 5.0),
                new GeoPoint2(10.0, 10.0)
            );

            Assert.Equal(0.0, poly.GetArea(), 12);
            Assert.Equal(0.0, poly.GetSignedArea(), 12);
        }

        [Fact]
        public void Polygon_Constructor_DropsRepeatedClosingVertex()
        {
            // Many drawing formats repeat the start vertex at the end to close the loop. The constructor must discard it,
            // otherwise the polygon will have a degenerate edge of length 0.
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 0.0));

            Assert.Equal(3, poly.VertexCount);
            Assert.Equal(3, poly.EdgeCount);
            Assert.Equal(8.0, poly.GetArea(), 12);
        }

        [Fact]
        public void Polygon_Constructor_RejectsNullAndTooFewVertices()
        {
            Assert.Throws<ArgumentNullException>(() => new GeoPolygon2((IEnumerable<GeoPoint2>)null));
            Assert.Throws<ArgumentException>(() => new GeoPolygon2(new GeoPoint2(0.0, 0.0)));
            Assert.Throws<ArgumentException>(
                () => new GeoPolygon2(new GeoPoint2(1.0, 1.0), new GeoPoint2(1.0, 1.0), new GeoPoint2(1.0, 1.0)));
        }

        [Fact]
        public void Polygon_Indexer_AndGetEdgeAt_RejectOutOfRange()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0));

            Assert.Throws<ArgumentOutOfRangeException>(() => poly[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => poly[3]);
            Assert.Throws<ArgumentOutOfRangeException>(() => poly.GetEdgeAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => poly.GetEdgeAt(3));
        }

        [Fact]
        public void Polygon_GetEdges_FormsClosedLoop()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0));

            var edges = poly.GetEdges().ToList();

            Assert.Equal(3, edges.Count);
            for (int i = 0; i < edges.Count; i++)
            {
                // The end point of this edge must match the start point of the next edge, with the last edge wrapping back to the first.
                Assert.Equal(edges[(i + 1) % edges.Count].StartPoint, edges[i].EndPoint);
            }
        }

        [Fact]
        public void Polygon_Equality_AndHashCode_WorkCorrectly()
        {
            var a = new GeoPolygon2(new GeoPoint2(0.0, 0.0), new GeoPoint2(4.0, 0.0), new GeoPoint2(4.0, 4.0));
            var b = new GeoPolygon2(new GeoPoint2(0.0, 0.0), new GeoPoint2(4.0, 0.0), new GeoPoint2(4.0, 4.0));
            var different = new GeoPolygon2(new GeoPoint2(0.0, 0.0), new GeoPoint2(5.0, 0.0), new GeoPoint2(5.0, 5.0));

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.False(a.Equals(different));
            Assert.False(a.Equals((GeoPolygon2)null));

            object notAPolygon = "not a GeoPolygon2";
            Assert.False(a.Equals(notAPolygon));
        }

        [Fact]
        public void Polygon_IntersectsWithLine_TouchingBoundaryCounts()
        {
            var poly = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0));

            // A line segment touching exactly one vertex still counts as an intersection.
            Assert.True(poly.CollidesWith(new GeoLine2(4.0, 4.0, 8.0, 8.0)));

            // A line segment running just outside the boundary does not.
            Assert.False(poly.CollidesWith(new GeoLine2(4.5, 0.0, 4.5, 4.0)));
        }

        [Fact]
        public void Polygon_IntersectsWithPolygon_ConcaveNotchIsNotOccupied()
        {
            // L-shaped polygon, the missing corner is at the top-right.
            var lShape = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 2.0),
                new GeoPoint2(2.0, 2.0),
                new GeoPoint2(2.0, 4.0),
                new GeoPoint2(0.0, 4.0));

            // Small polygon fits entirely inside the missing corner: inside bounding box but does NOT intersect the L-shape.
            var inNotch = new GeoPolygon2(
                new GeoPoint2(2.5, 2.5),
                new GeoPoint2(3.5, 2.5),
                new GeoPoint2(3.5, 3.5),
                new GeoPoint2(2.5, 3.5));

            Assert.False(lShape.CollidesWith(inNotch));
            Assert.False(inNotch.CollidesWith(lShape));
        }

        [Fact]
        public void Polygon_ClockwiseAndCounterClockwise_BehaveIdenticallyForContainment()
        {
            var ccw = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0));

            var cw = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(0.0, 4.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(4.0, 0.0));

            Assert.False(ccw.IsClockwise());
            Assert.True(cw.IsClockwise());

            // Vertices traversal direction must not affect containment checks or absolute area.
            Assert.Equal(ccw.GetArea(), cw.GetArea(), 12);
            Assert.Equal(ccw.Contains(new GeoPoint2(2.0, 2.0)), cw.Contains(new GeoPoint2(2.0, 2.0)));
            Assert.Equal(ccw.Contains(new GeoPoint2(9.0, 9.0)), cw.Contains(new GeoPoint2(9.0, 9.0)));
        }
        [Fact]
        public void Polygon_EqualityOperators_CompareByValue()
        {
            var a = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(1, 1));
            var b = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(1, 1));
            var c = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(2, 0), new GeoPoint2(2, 2));

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a != c);

            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            // Null handling matches GeoPolyline2.
            Assert.True((GeoPolygon2)null == (GeoPolygon2)null);
            Assert.False(a == null);
            Assert.False(null == a);
        }

        [Fact]
        public void Polygon_Constructor_DropsConsecutiveDuplicateVertices()
        {
            // A repeated vertex would otherwise leave a zero-length edge behind.
            var poly = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(0, 0),
                new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 10));

            Assert.Equal(3, poly.VertexCount);
            Assert.Equal(50.0, poly.GetArea(), 9);

            // Once the duplicates are removed there are not enough distinct vertices left.
            Assert.Throws<ArgumentException>(() =>
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(1, 1)));
        }

        [Fact]
        public void Polygon_TranslateAndRotate_ProduceMovedCopies()
        {
            var poly = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            var moved = poly.Translate(new GeoVector2(5, -5));
            Assert.True(moved[0].IsEqualTo(new GeoPoint2(5, -5)));
            Assert.Equal(poly.GetArea(), moved.GetArea(), 9);
            Assert.Equal(moved, poly + new GeoVector2(5, -5));
            Assert.Equal(poly, moved - new GeoVector2(5, -5));

            var rotated = poly.RotateBy(Math.PI / 2.0, new GeoPoint2(0, 0));
            Assert.True(rotated[1].IsEqualTo(new GeoPoint2(0, 10)));
            Assert.Equal(poly.GetArea(), rotated.GetArea(), 9);
        }

        [Fact]
        public void Polygon_ToPolyline_ProducesClosedPolylineBoundary()
        {
            // Create a triangle polygon with 3 vertices
            var p0 = new GeoPoint2(0, 0);
            var p1 = new GeoPoint2(10, 0);
            var p2 = new GeoPoint2(10, 10);
            var polygon = new GeoPolygon2(p0, p1, p2);

            // Convert to polyline
            GeoPolyline2 polyline = polygon.ToPolyline();

            // The resulting polyline should have 4 vertices (closed boundary)
            Assert.NotNull(polyline);
            Assert.Equal(4, polyline.VertexCount);

            // Check that vertices match in order and end vertex repeats the first one
            Assert.True(polyline[0].IsEqualTo(p0));
            Assert.True(polyline[1].IsEqualTo(p1));
            Assert.True(polyline[2].IsEqualTo(p2));
            Assert.True(polyline[3].IsEqualTo(p0));

            // Cumulative length of the polyline should equal the perimeter of the polygon
            Assert.Equal(polygon.Length, polyline.Length, 9);
        }

        [Fact]
        public void Polygon_GetClosestOnBoundary_WorksCorrectly()
        {
            var poly1 = new GeoPolygon2(
                new GeoPoint2(0.0, 0.0),
                new GeoPoint2(4.0, 0.0),
                new GeoPoint2(4.0, 4.0),
                new GeoPoint2(0.0, 4.0)
            );

            var poly2 = new GeoPolygon2(
                new GeoPoint2(10.0, 0.0),
                new GeoPoint2(14.0, 0.0),
                new GeoPoint2(14.0, 4.0),
                new GeoPoint2(10.0, 4.0)
            );

            // Test 1: Polygon - Polygon
            // The facing edges are parallel and overlap over their whole height, so every pair between
            // them is 6 apart; the segment is anchored at the middle of that overlap.
            var segPolys = poly1.GetClosestOnBoundary(poly2);
            Assert.Equal(6.0, segPolys.Length, 9);
            Assert.True(segPolys.StartPoint.IsEqualTo(new GeoPoint2(4.0, 2.0)));
            Assert.True(segPolys.EndPoint.IsEqualTo(new GeoPoint2(10.0, 2.0)));

            // Test 2: Polygon - Line
            var line = new GeoLine2(4.0, 8.0, 4.0, 12.0);
            var segLine = poly1.GetClosestOnBoundary(line);
            Assert.Equal(4.0, segLine.Length, 9);
            Assert.True(segLine.StartPoint.IsEqualTo(new GeoPoint2(4.0, 4.0)));
            Assert.True(segLine.EndPoint.IsEqualTo(new GeoPoint2(4.0, 8.0)));
        }
    }
}

