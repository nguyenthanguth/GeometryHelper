using System;
using System.Collections.Generic;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    public class EnumerableExtensionTests
    {
        private static List<TSG.Point> OnX(params double[] xs)
        {
            var points = new List<TSG.Point>();
            foreach (double x in xs)
            {
                points.Add(new TSG.Point(x, 0.0, 0.0));
            }

            return points;
        }

        private static TSG.LineSegment Segment(double sx, double sy, double sz, double ex, double ey, double ez)
        {
            return new TSG.LineSegment(new TSG.Point(sx, sy, sz), new TSG.Point(ex, ey, ez));
        }

        #region ToLineSegments

        [Fact]
        public void ToLineSegments_ChainsConsecutivePairs()
        {
            List<TSG.LineSegment> segments = OnX(0.0, 1.0, 3.0).ToLineSegments();

            Assert.Equal(2, segments.Count);
            Assert.Equal(0.0, segments[0].StartPoint.X, 12);
            Assert.Equal(1.0, segments[0].EndPoint.X, 12);
            Assert.Equal(1.0, segments[1].StartPoint.X, 12);
            Assert.Equal(3.0, segments[1].EndPoint.X, 12);
        }

        [Fact]
        public void ToLineSegments_LeavesTheChainOpen()
        {
            // First and last point coincide, so the ring is explicitly closed by the caller. The helper
            // must not add a further closing segment on top of that.
            List<TSG.LineSegment> segments = OnX(0.0, 1.0, 2.0, 0.0).ToLineSegments();

            Assert.Equal(3, segments.Count);
            Assert.Equal(segments[0].StartPoint.X, segments[segments.Count - 1].EndPoint.X, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ToLineSegments_YieldsNothingBelowTwoPoints(int count)
        {
            var points = new List<TSG.Point>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new TSG.Point(i, 0.0, 0.0));
            }

            Assert.Empty(points.ToLineSegments());
        }

        [Fact]
        public void ToLineSegments_KeepsRepeatedPointsAsZeroLengthSegments()
        {
            // Documented behaviour: filtering belongs to RemoveConsecutiveNearPoints, not here.
            List<TSG.LineSegment> segments = OnX(0.0, 0.0, 1.0).ToLineSegments();

            Assert.Equal(2, segments.Count);
            Assert.Equal(0.0, segments[0].Length(), 12);
        }

        [Fact]
        public void ToLineSegments_DoesNotMutateTheInput()
        {
            List<TSG.Point> points = OnX(0.0, 1.0, 2.0);

            points.ToLineSegments();

            Assert.Equal(3, points.Count);
        }

        [Fact]
        public void ToLineSegments_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ((List<TSG.Point>)null).ToLineSegments());
        }

        #endregion

        #region RemoveConsecutiveNearPoints

        [Fact]
        public void RemoveConsecutiveNearPoints_ReAnchorsInsteadOfSwallowingTheRun()
        {
            // Every neighbour is 0.6 apart, under the tolerance, but the run must not collapse to one
            // point: each survivor becomes the new anchor, so the chain thins rather than vanishes.
            List<TSG.Point> result = OnX(0.0, 0.6, 1.2, 1.8, 2.4).RemoveConsecutiveNearPoints(1.0);

            Assert.Equal(3, result.Count);
            Assert.Equal(0.0, result[0].X, 12);
            Assert.Equal(1.2, result[1].X, 12);
            Assert.Equal(2.4, result[2].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_LeavesNoConsecutivePairWithinTolerance()
        {
            List<TSG.Point> result = OnX(0.0, 0.1, 0.2, 0.3, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(1.0);

            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(TSG.Distance.PointToPoint(result[i - 1], result[i]) > 1.0);
            }
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_MeasuresInThreeDimensions()
        {
            // The two points share X and Y and differ only in Z, so a purely planar comparison would
            // wrongly fuse them.
            var points = new List<TSG.Point>
            {
                new TSG.Point(0.0, 0.0, 0.0),
                new TSG.Point(0.0, 0.0, 5.0),
            };

            Assert.Equal(2, points.RemoveConsecutiveNearPoints(1.0).Count);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DropsTheLastPointWhenItIsTooClose()
        {
            // The end of the chain is not privileged: 5.4 sits within 1.0 of 5.0 and goes, which shortens
            // the chain. Callers that need the original endpoint have to re-append it themselves.
            List<TSG.Point> result = OnX(0.0, 5.0, 5.4).RemoveConsecutiveNearPoints(1.0);

            Assert.Equal(2, result.Count);
            Assert.Equal(5.0, result[result.Count - 1].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_TreatsTheToleranceItselfAsCoincident()
        {
            // The kept-point test is "distance > tolerance", so a gap of exactly the tolerance is removed.
            List<TSG.Point> result = OnX(0.0, 1.0).RemoveConsecutiveNearPoints(1.0);

            Assert.Single(result);
            Assert.Equal(0.0, result[0].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_KeepsPointsFartherThanTheTolerance()
        {
            List<TSG.Point> result = OnX(0.0, 1.0, 2.0).RemoveConsecutiveNearPoints(0.5);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_WithZeroToleranceRemovesOnlyExactRepeats()
        {
            List<TSG.Point> result = OnX(0.0, 0.0, 1.0, 1.0, 2.0).RemoveConsecutiveNearPoints(0.0);

            Assert.Equal(3, result.Count);
            Assert.Equal(0.0, result[0].X, 12);
            Assert.Equal(1.0, result[1].X, 12);
            Assert.Equal(2.0, result[2].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_AlwaysKeepsTheFirstPoint()
        {
            List<TSG.Point> result = OnX(7.0, 7.0, 7.0).RemoveConsecutiveNearPoints(1.0);

            Assert.Single(result);
            Assert.Equal(7.0, result[0].X, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RemoveConsecutiveNearPoints_CopiesShortListsThrough(int count)
        {
            var points = new List<TSG.Point>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new TSG.Point(i, 0.0, 0.0));
            }

            List<TSG.Point> result = points.RemoveConsecutiveNearPoints(1.0);

            Assert.Equal(count, result.Count);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DoesNotMutateTheInput()
        {
            List<TSG.Point> points = OnX(0.0, 0.1, 0.2);

            List<TSG.Point> result = points.RemoveConsecutiveNearPoints(1.0);

            Assert.Equal(3, points.Count);
            Assert.Single(result);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => ((List<TSG.Point>)null).RemoveConsecutiveNearPoints(1.0));
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_FeedsToLineSegmentsWithoutZeroLengthEdges()
        {
            List<TSG.LineSegment> segments = OnX(0.0, 0.1, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(1.0)
                .ToLineSegments();

            Assert.Equal(2, segments.Count);
            foreach (TSG.LineSegment segment in segments)
            {
                Assert.True(segment.Length() > 1.0);
            }
        }

        #endregion

        #region GetLongestLength

        [Fact]
        public void GetLongestLength_ReturnsTheLongestSegment()
        {
            var segments = new List<TSG.LineSegment>
            {
                Segment(0, 0, 0, 1, 0, 0),
                Segment(0, 0, 0, 7, 0, 0),
                Segment(0, 0, 0, 3, 0, 0),
            };

            Assert.Equal(7.0, segments.GetLongestLength().Length(), 12);
        }

        [Fact]
        public void GetLongestLength_MeasuresInThreeDimensions()
        {
            // In plan the flat segment is the longer one (5 against 4), but in space the sloped segment
            // wins at about 5.66. Measuring in 2D would pick the wrong one.
            TSG.LineSegment flat = Segment(0, 0, 0, 5, 0, 0);
            TSG.LineSegment sloped = Segment(0, 0, 0, 4, 0, 4);

            TSG.LineSegment longest = new List<TSG.LineSegment> { flat, sloped }.GetLongestLength();

            Assert.Equal(sloped.Length(), longest.Length(), 12);
            Assert.Equal(4.0, longest.EndPoint.Z, 12);
        }

        [Fact]
        public void GetLongestLength_KeepsTheEarlierSegmentOnATie()
        {
            TSG.LineSegment first = Segment(0, 0, 0, 5, 0, 0);
            TSG.LineSegment second = Segment(0, 10, 0, 5, 10, 0);

            TSG.LineSegment longest = new List<TSG.LineSegment> { first, second }.GetLongestLength();

            Assert.Equal(0.0, longest.StartPoint.Y, 12);
        }

        [Fact]
        public void GetLongestLength_WithASingleSegmentReturnsIt()
        {
            var segments = new List<TSG.LineSegment> { Segment(0, 0, 0, 2, 0, 0) };

            Assert.Equal(2.0, segments.GetLongestLength().Length(), 12);
        }

        [Fact]
        public void GetLongestLength_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => ((List<TSG.LineSegment>)null).GetLongestLength());
        }

        [Fact]
        public void GetLongestLength_ThrowsOnAnEmptyList()
        {
            Assert.Throws<InvalidOperationException>(
                () => new List<TSG.LineSegment>().GetLongestLength());
        }

        #endregion
    }
}
