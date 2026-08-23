using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    public class ContainmentTests
    {
        #region IsPointOn Tests

        [Theory]
        [InlineData(5, 0, true)]                       // Point on interior
        [InlineData(0, 0, true)]                       // Point at StartPoint
        [InlineData(10, 0, true)]                      // Point at EndPoint
        [InlineData(-1, 0, false)]                     // Collinear before StartPoint
        [InlineData(11, 0, false)]                     // Collinear after EndPoint
        [InlineData(5, 0.1, false)]                    // Parallel2 offset
        public void IsPointOn_Line_ComprehensiveCases(double px, double py, bool expected)
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            Assert.Equal(expected, Containment2.IsPointOn(line, new GeoPoint2(px, py)));
        }

        [Theory]
        [InlineData(10, 0, true)]                      // East cardinal point on boundary
        [InlineData(0, 10, true)]                      // North cardinal point on boundary
        [InlineData(-10, 0, true)]                     // West cardinal point on boundary
        [InlineData(0, -10, true)]                     // South cardinal point on boundary
        [InlineData(0, 0, false)]                      // Center (not on boundary)
        [InlineData(5, 5, false)]                      // Inside (not on boundary)
        [InlineData(15, 0, false)]                     // Outside
        public void IsPointOn_Circle_ComprehensiveCases(double px, double py, bool expected)
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);
            Assert.Equal(expected, Containment2.IsPointOn(circle, new GeoPoint2(px, py)));
        }

        [Theory]
        [InlineData(5, 0, true)]                       // Bottom edge
        [InlineData(10, 5, true)]                      // Right edge
        [InlineData(5, 10, true)]                      // Top edge
        [InlineData(0, 5, true)]                       // Left edge
        [InlineData(0, 0, true)]                       // Vertex
        [InlineData(5, 5, false)]                      // Interior center
        [InlineData(15, 5, false)]                     // Exterior
        public void IsPointOn_Polygon_ComprehensiveCases(double px, double py, bool expected)
        {
            var poly = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(0, 10)
            });
            Assert.Equal(expected, Containment2.IsPointOn(poly, new GeoPoint2(px, py)));
        }

        [Theory]
        [InlineData(5, 0, true)]                       // On segment 1
        [InlineData(10, 5, true)]                      // On segment 2
        [InlineData(15, 10, true)]                     // On segment 3
        [InlineData(10, 0, true)]                      // On bend vertex 1
        [InlineData(10, 10, true)]                     // On bend vertex 2
        [InlineData(5, 5, false)]                      // Off polyline
        public void IsPointOn_Polyline_ComprehensiveCases(double px, double py, bool expected)
        {
            var pl = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(20, 10)
            });
            Assert.Equal(expected, Containment2.IsPointOn(pl, new GeoPoint2(px, py)));
        }

        #endregion

        #region Locate Tests

        [Theory]
        [InlineData(0, 0, PointLocation.Inside)]       // Circle center
        [InlineData(6, 0, PointLocation.Inside)]       // Inside interior
        [InlineData(10, 0, PointLocation.OnSide)]      // Circumference
        [InlineData(10.000001, 0, PointLocation.OnSide)]// Within default tolerance
        [InlineData(11, 0, PointLocation.OutSide)]     // Outside
        public void Locate_Circle_InsideOnAndOutside(double px, double py, PointLocation expected)
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);
            Assert.Equal(expected, Containment2.Locate(circle, new GeoPoint2(px, py)));
        }

        [Theory]
        [InlineData(0, 0, PointLocation.Inside)]       // Center
        [InlineData(10, 0, PointLocation.OnSide)]      // Right edge
        [InlineData(-10, 0, PointLocation.OnSide)]     // Left edge
        [InlineData(0, 5, PointLocation.OnSide)]       // Top edge
        [InlineData(0, -5, PointLocation.OnSide)]      // Bottom edge
        [InlineData(10, 5, PointLocation.OnSide)]      // Top-right corner
        [InlineData(-10, -5, PointLocation.OnSide)]    // Bottom-left corner
        [InlineData(15, 0, PointLocation.OutSide)]     // Outside
        public void Locate_Rectangle_InsideEdgesCornersOutside(double px, double py, PointLocation expected)
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 10, 0);
            Assert.Equal(expected, Containment2.Locate(rect, new GeoPoint2(px, py)));
        }

        [Fact]
        public void Locate_Polygon_ConvexAndConcave()
        {
            // Concave polygon with a V-shaped indentation on top edge
            var concave = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(20, 0),
                new GeoPoint2(20, 20),
                new GeoPoint2(10, 10), // V-notch vertex inside
                new GeoPoint2(0, 20)
            });

            // Strictly inside solid region
            Assert.Equal(PointLocation.Inside, Containment2.Locate(concave, new GeoPoint2(5, 5)));
            Assert.Equal(PointLocation.Inside, Containment2.Locate(concave, new GeoPoint2(15, 5)));

            // On notch vertex
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(concave, new GeoPoint2(10, 10)));

            // In the empty notch bay (outside polygon but within bounding box)
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(concave, new GeoPoint2(10, 15)));

            // Completely outside
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(concave, new GeoPoint2(-5, 0)));
        }

        [Theory]
        [InlineData(5, 0, PointLocation.OnSide)]       // On segment 1
        [InlineData(10, 0, PointLocation.OnSide)]      // On vertex
        [InlineData(10, 5, PointLocation.OnSide)]      // On segment 2
        [InlineData(5, 5, PointLocation.OutSide)]      // Outside
        public void Locate_Polyline_SegmentsVerticesOutside(double px, double py, PointLocation expected)
        {
            var pl = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            });
            Assert.Equal(expected, Containment2.Locate(pl, new GeoPoint2(px, py)));
        }

        [Theory]
        [InlineData(5, 0, PointLocation.OnSide)]       // On line midpoint
        [InlineData(0, 0, PointLocation.OnSide)]       // On line startpoint
        [InlineData(10, 0, PointLocation.OnSide)]      // On line endpoint
        [InlineData(5, 5, PointLocation.OutSide)]      // Off line
        public void Locate_Line_MidpointEndpointsOutside(double px, double py, PointLocation expected)
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            Assert.Equal(expected, Containment2.Locate(line, new GeoPoint2(px, py)));
        }

        #endregion

        #region Shape Contains Sub-Shapes Tests

        [Fact]
        public void Contains_Rectangle_LineAndPolyline()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 20, 0);

            // Fully inside line
            var lInside = new GeoLine2(new GeoPoint2(-5, -5), new GeoPoint2(5, 5));
            Assert.True(Containment2.Contains(rect, lInside));

            // Line touching boundary endpoints
            var lTouching = new GeoLine2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0));
            Assert.True(Containment2.Contains(rect, lTouching));

            // Line partially protruding outside
            var lProtruding = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(15, 0));
            Assert.False(Containment2.Contains(rect, lProtruding));

            // Polyline inside
            var plInside = new GeoPolyline2(new[]
            {
                new GeoPoint2(-5, -5),
                new GeoPoint2(0, -5),
                new GeoPoint2(0, 5)
            });
            Assert.True(Containment2.Contains(rect, plInside));
        }

        [Fact]
        public void Contains_Circle_LineAndCircle()
        {
            var cOuter = new GeoCircle2(new GeoPoint2(0, 0), 10);

            // Inner circle fully contained
            var cInner = new GeoCircle2(new GeoPoint2(2, 2), 3);
            Assert.True(Containment2.Contains(cOuter, cInner));

            // Inner circle touching boundary internally
            var cTangentInner = new GeoCircle2(new GeoPoint2(5, 0), 5);
            Assert.True(Containment2.Contains(cOuter, cTangentInner));

            // Overlapping circle protruding outside
            var cOverlap = new GeoCircle2(new GeoPoint2(8, 0), 5);
            Assert.False(Containment2.Contains(cOuter, cOverlap));

            // Line segment inside circle
            var lInside = new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(5, 0));
            Assert.True(Containment2.Contains(cOuter, lInside));
        }

        [Fact]
        public void Contains_Polygon_LinePolylineAndNestedShapes()
        {
            var poly = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(20, 0),
                new GeoPoint2(20, 20),
                new GeoPoint2(0, 20)
            });

            // Line segment inside
            var lInside = new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(15, 15));
            Assert.True(Containment2.Contains(poly, lInside));

            // Line segment cutting through boundary
            var lCross = new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(25, 5));
            Assert.False(Containment2.Contains(poly, lCross));

            // Polyline zigzag inside
            var plZigzag = new GeoPolyline2(new[]
            {
                new GeoPoint2(2, 2),
                new GeoPoint2(18, 2),
                new GeoPoint2(2, 18),
                new GeoPoint2(18, 18)
            });
            Assert.True(Containment2.Contains(poly, plZigzag));
        }

        #endregion
        #region Tolerance Propagation Regression

        [Fact]
        public void ContainsLine_HonoursTheSuppliedTolerance()
        {
            var loose = new Tolerance(5.0, 5.0);

            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);
            var pastTheRim = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(13, 0));
            Assert.False(Containment2.Contains(circle, pastTheRim));
            Assert.True(Containment2.Contains(circle, pastTheRim, loose));

            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 10, 10);
            var pastTheEdge = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(6, 0));
            Assert.False(Containment2.Contains(rect, pastTheEdge));
            Assert.True(Containment2.Contains(rect, pastTheEdge, loose));
        }

        [Fact]
        public void ContainsPoint_Rectangle_HonoursTheSuppliedTolerance()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 10, 10);
            var justOutside = new GeoPoint2(6, 0);

            Assert.False(Containment2.Contains(rect, justOutside));
            Assert.True(Containment2.Contains(rect, justOutside, new Tolerance(2.0, 2.0)));
        }

        #endregion

        #region Polygon Contains Line Regression

        [Fact]
        public void ContainsLine_DiagonalBetweenVertices_IsContained()
        {
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0),
                new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            // The diagonal touches the boundary at both ends but never leaves the square. Treating any
            // crossing as a failure used to reject it.
            Assert.True(Containment2.Contains(square, new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10))));

            // A segment lying along an edge is contained for the same reason.
            Assert.True(Containment2.Contains(square, new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0))));

            // A segment that genuinely leaves is not.
            Assert.False(Containment2.Contains(square, new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(20, 5))));
        }

        [Fact]
        public void ContainsLine_ConcavePolygon_RejectsSegmentsCrossingTheNotch()
        {
            // An L shape: both endpoints sit inside, but the straight path between them exits the notch.
            var lShape = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 4),
                new GeoPoint2(4, 4), new GeoPoint2(4, 10), new GeoPoint2(0, 10));

            Assert.True(Containment2.Contains(lShape, new GeoPoint2(9, 2)));
            Assert.True(Containment2.Contains(lShape, new GeoPoint2(2, 9)));
            Assert.False(Containment2.Contains(lShape, new GeoLine2(new GeoPoint2(9, 2), new GeoPoint2(2, 9))));

            // A segment staying within one arm is contained.
            Assert.True(Containment2.Contains(lShape, new GeoLine2(new GeoPoint2(1, 1), new GeoPoint2(9, 1))));
        }

        #endregion

        #region Polyline Contains Point

        [Fact]
        public void Polyline_HoldsOnlyThePointsOnItsPath()
        {
            // A chain of vertices tracing a square is still a curve, not the square it draws. There is no
            // Contains overload for a polyline at all, because it would only ever restate IsPointOn.
            var traced = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));
            var open = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            var inside = new GeoPoint2(5, 5);
            var onPath = new GeoPoint2(5, 0);
            var outside = new GeoPoint2(50, 50);

            Assert.False(Containment2.IsPointOn(traced, inside));
            Assert.True(Containment2.IsPointOn(traced, onPath));
            Assert.False(Containment2.IsPointOn(traced, outside));

            Assert.False(Containment2.IsPointOn(open, inside));
            Assert.True(Containment2.IsPointOn(open, onPath));
            Assert.False(Containment2.IsPointOn(open, outside));

            Assert.Equal(PointLocation.OutSide, Containment2.Locate(traced, inside));
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(traced, onPath));

            // Converting to a polygon is what brings the enclosed area into play.
            Assert.True(Containment2.Contains(traced.ToPolygon(), inside));
            Assert.Equal(PointLocation.Inside, Containment2.Locate(traced.ToPolygon(), inside));
        }

        #endregion
    }
}
