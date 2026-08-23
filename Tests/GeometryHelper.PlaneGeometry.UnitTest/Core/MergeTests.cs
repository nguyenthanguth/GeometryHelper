using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    public class MergeTests
    {
        #region ConsecutiveLines Tests

        [Fact]
        public void ConsecutiveLines_EmptyInput_ReturnsEmptyArray()
        {
            var segments = new List<GeoLine2>();
            var result = Merge2.ConsecutiveLines(segments, Tolerance.Global);
            Assert.Empty(result);
        }

        [Fact]
        public void ConsecutiveLines_SingleSegment_ReturnsSameSegment()
        {
            var segment = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var segments = new[] { segment };

            var result = Merge2.ConsecutiveLines(segments, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(segment.StartPoint, result[0].StartPoint);
            Assert.Equal(segment.EndPoint, result[0].EndPoint);
        }

        [Fact]
        public void ConsecutiveLines_ContinuousCollinearSegments_MergesIntoSingleSegment()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(15, 0))
            };

            var result = Merge2.ConsecutiveLines(segments, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(new GeoPoint2(0, 0), result[0].StartPoint);
            Assert.Equal(new GeoPoint2(15, 0), result[0].EndPoint);
        }

        [Fact]
        public void ConsecutiveLines_DisjointSegments_DoesNotMerge()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(4, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(10, 0))
            };

            var result = Merge2.ConsecutiveLines(segments, Tolerance.Global);

            Assert.Equal(2, result.Length);
            Assert.Equal(new GeoPoint2(0, 0), result[0].StartPoint);
            Assert.Equal(new GeoPoint2(4, 0), result[0].EndPoint);
            Assert.Equal(new GeoPoint2(5, 0), result[1].StartPoint);
            Assert.Equal(new GeoPoint2(10, 0), result[1].EndPoint);
        }

        [Fact]
        public void ConsecutiveLines_MixedContinuousAndDisjointSegments_MergesOnlyContinuous()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(12, 0), new GeoPoint2(15, 0)), // Disjoint
                new GeoLine2(new GeoPoint2(15, 0), new GeoPoint2(20, 0))  // Continuous from previous
            };

            var result = Merge2.ConsecutiveLines(segments, Tolerance.Global);

            Assert.Equal(2, result.Length);
            Assert.Equal(new GeoPoint2(0, 0), result[0].StartPoint);
            Assert.Equal(new GeoPoint2(10, 0), result[0].EndPoint);
            Assert.Equal(new GeoPoint2(12, 0), result[1].StartPoint);
            Assert.Equal(new GeoPoint2(20, 0), result[1].EndPoint);
        }

        [Fact]
        public void ConsecutiveLines_ToleranceCheck_MergesWithinTolerance()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5.05, 0), new GeoPoint2(10, 0)) // gap is 0.05
            };

            // Custom tolerance smaller than gap
            var strictTolerance = new Tolerance(0.01, 0.01);
            var resultStrict = Merge2.ConsecutiveLines(segments, strictTolerance);
            Assert.Equal(2, resultStrict.Length);

            // Custom tolerance larger than gap
            var looseTolerance = new Tolerance(0.1, 0.1);
            var resultLoose = Merge2.ConsecutiveLines(segments, looseTolerance);
            Assert.Single(resultLoose);
            Assert.Equal(new GeoPoint2(0, 0), resultLoose[0].StartPoint);
            Assert.Equal(new GeoPoint2(10, 0), resultLoose[0].EndPoint);
        }

        [Fact]
        public void Join_WithGaps_DoesNotMerge()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(7, 0), new GeoPoint2(12, 0)),  // Gap from 5 to 7
                new GeoLine2(new GeoPoint2(15, 0), new GeoPoint2(20, 0))  // Gap from 12 to 15
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Equal(3, result.Length);
            Assert.Equal(2, result[0].VertexCount);
            Assert.Equal(2, result[1].VertexCount);
            Assert.Equal(2, result[2].VertexCount);
        }

        [Fact]
        public void Join_CollinearTouching_MergesAndSimplifies()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(12, 0)),  // Touches at (5,0), extends the run
                new GeoLine2(new GeoPoint2(12, 0), new GeoPoint2(17, 0))  // Touches at (12,0), extends the run
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(2, result[0].VertexCount); // Junctions removed because collinear
            Assert.Equal(new GeoPoint2(0, 0), result[0][0]);
            Assert.Equal(new GeoPoint2(17, 0), result[0][1]);
        }

        [Fact]
        public void Join_CollinearFoldingBack_KeepsTheTurningPoint()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(12, 0)),  // Touches at (5,0), extends the run
                new GeoLine2(new GeoPoint2(12, 0), new GeoPoint2(7, 0))   // Touches at (12,0), doubles back
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            // (5,0) goes because the run passes straight through it, but (12,0) stays: the run turns
            // around there. Dropping it would hand back a path 7 long in place of one 17 long.
            Assert.Single(result);
            Assert.Equal(3, result[0].VertexCount);
            Assert.Equal(new GeoPoint2(0, 0), result[0][0]);
            Assert.Equal(new GeoPoint2(12, 0), result[0][1]);
            Assert.Equal(new GeoPoint2(7, 0), result[0][2]);
            Assert.Equal(17.0, result[0].Length, 9);
        }

        [Fact]
        public void Join_NonCollinearTouching_MergesIntoPolylineWithBend()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5)),    // Touches at (5,0), bend
                new GeoLine2(new GeoPoint2(10, 5), new GeoPoint2(5, 5))   // Touches at (5,5), bend
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(4, result[0].VertexCount); // Keeps all vertices
            Assert.Equal(new GeoPoint2(0, 0), result[0][0]);
            Assert.Equal(new GeoPoint2(5, 0), result[0][1]);
            Assert.Equal(new GeoPoint2(5, 5), result[0][2]);
            Assert.Equal(new GeoPoint2(10, 5), result[0][3]);
        }

        #endregion

        #region Polylines Tests

        [Fact]
        public void Polylines_DisjointPolylines_ReturnsNull()
        {
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0));
            var second = new GeoPolyline2(new GeoPoint2(6, 0), new GeoPoint2(10, 0));

            var result = Merge2.Polylines(first, second, Tolerance.Global);

            Assert.Null(result);
        }

        [Fact]
        public void Polylines_ContinuousWithBend_MergesAndKeepsJunction()
        {
            // Creates a L-shape, which has a bend at (5, 0)
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0));
            var second = new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(5, 5));

            var result = Merge2.Polylines(first, second, Tolerance.Global);

            Assert.NotNull(result);
            Assert.Equal(3, result.VertexCount);
            Assert.Equal(new GeoPoint2(0, 0), result[0]);
            Assert.Equal(new GeoPoint2(5, 0), result[1]); // Junction kept because of bend
            Assert.Equal(new GeoPoint2(5, 5), result[2]);
        }

        [Fact]
        public void Polylines_ContinuousWithoutBend_MergesAndRemovesJunction()
        {
            // A straight line split in two, collinear at (5, 0)
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0));
            var second = new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(10, 0));

            var result = Merge2.Polylines(first, second, Tolerance.Global);

            Assert.NotNull(result);
            Assert.Equal(2, result.VertexCount); // Junction (5, 0) removed because collinear
            Assert.Equal(new GeoPoint2(0, 0), result[0]);
            Assert.Equal(new GeoPoint2(10, 0), result[1]);
        }

        [Fact]
        public void Polylines_ToleranceCheck_MergesWithinTolerance()
        {
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0));
            var second = new GeoPolyline2(new GeoPoint2(5.05, 0), new GeoPoint2(10, 0));

            var strictTolerance = new Tolerance(0.01, 0.01);
            Assert.Null(Merge2.Polylines(first, second, strictTolerance));

            var looseTolerance = new Tolerance(0.1, 0.1);
            var result = Merge2.Polylines(first, second, looseTolerance);
            Assert.NotNull(result);
            Assert.Equal(2, result.VertexCount);
        }

        #endregion

        #region ConsecutivePolylines Tests

        [Fact]
        public void ConsecutivePolylines_EmptyInput_ReturnsEmptyArray()
        {
            var polylines = new List<GeoPolyline2>();
            var result = Merge2.ConsecutivePolylines(polylines, Tolerance.Global);
            Assert.Empty(result);
        }

        [Fact]
        public void ConsecutivePolylines_SinglePolyline_ReturnsSamePolyline()
        {
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 5));
            var polylines = new[] { polyline };

            var result = Merge2.ConsecutivePolylines(polylines, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(polyline, result[0]);
        }

        [Fact]
        public void ConsecutivePolylines_ContinuousPolylines_MergesIntoSinglePolyline()
        {
            var polylines = new[]
            {
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(5, 5)), // bend
                new GeoPolyline2(new GeoPoint2(5, 5), new GeoPoint2(10, 5)) // bend
            };

            var result = Merge2.ConsecutivePolylines(polylines, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(4, result[0].VertexCount);
            Assert.Equal(new GeoPoint2(0, 0), result[0][0]);
            Assert.Equal(new GeoPoint2(5, 0), result[0][1]);
            Assert.Equal(new GeoPoint2(5, 5), result[0][2]);
            Assert.Equal(new GeoPoint2(10, 5), result[0][3]);
        }

        [Fact]
        public void ConsecutivePolylines_DisjointPolylines_DoesNotMerge()
        {
            var polylines = new[]
            {
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoPolyline2(new GeoPoint2(6, 0), new GeoPoint2(10, 0))
            };

            var result = Merge2.ConsecutivePolylines(polylines, Tolerance.Global);

            Assert.Equal(2, result.Length);
        }

        [Fact]
        public void ConsecutivePolylines_MixedContinuousAndDisjoint_MergesOnlyContinuous()
        {
            var polylines = new[]
            {
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoPolyline2(new GeoPoint2(5, 0), new GeoPoint2(5, 5)),
                new GeoPolyline2(new GeoPoint2(7, 7), new GeoPoint2(10, 10)), // disjoint
                new GeoPolyline2(new GeoPoint2(10, 10), new GeoPoint2(13, 13)) // continuous (collinear at 10,10)
            };

            var result = Merge2.ConsecutivePolylines(polylines, Tolerance.Global);

            Assert.Equal(2, result.Length);
            
            // First merged stretch
            Assert.Equal(3, result[0].VertexCount);
            Assert.Equal(new GeoPoint2(0, 0), result[0][0]);
            Assert.Equal(new GeoPoint2(5, 5), result[0][2]);

            // Second merged stretch
            Assert.Equal(2, result[1].VertexCount); // collinear at junction
            Assert.Equal(new GeoPoint2(7, 7), result[1][0]);
            Assert.Equal(new GeoPoint2(13, 13), result[1][1]);
        }

        #endregion
    }
}
