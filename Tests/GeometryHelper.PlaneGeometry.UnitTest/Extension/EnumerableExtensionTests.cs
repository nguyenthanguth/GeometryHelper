using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Extension;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Extension
{
    public class EnumerableExtensionTests
    {
        private static Tolerance Tol(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

        private static List<GeoPoint2> OnX(params double[] xs)
        {
            var points = new List<GeoPoint2>();
            foreach (double x in xs)
            {
                points.Add(new GeoPoint2(x, 0.0));
            }

            return points;
        }

        #region ToGeoLine2s

        [Fact]
        public void ToGeoLine2s_ChainsConsecutivePairs()
        {
            List<GeoLine2> segments = OnX(0.0, 1.0, 3.0).ToGeoLine2s();

            Assert.Equal(2, segments.Count);
            Assert.Equal(new GeoPoint2(0.0, 0.0), segments[0].StartPoint);
            Assert.Equal(new GeoPoint2(1.0, 0.0), segments[0].EndPoint);
            Assert.Equal(new GeoPoint2(1.0, 0.0), segments[1].StartPoint);
            Assert.Equal(new GeoPoint2(3.0, 0.0), segments[1].EndPoint);
        }

        [Fact]
        public void ToGeoLine2s_LeavesTheChainOpen()
        {
            // First and last vertex coincide, so the ring is explicitly closed by the caller. The helper
            // must not add a further closing segment on top of that.
            List<GeoLine2> segments = OnX(0.0, 1.0, 2.0, 0.0).ToGeoLine2s();

            Assert.Equal(3, segments.Count);
            Assert.Equal(segments[0].StartPoint, segments[segments.Count - 1].EndPoint);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ToGeoLine2s_YieldsNothingBelowTwoPoints(int count)
        {
            var points = new List<GeoPoint2>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint2(i, 0.0));
            }

            Assert.Empty(points.ToGeoLine2s());
        }

        [Fact]
        public void ToGeoLine2s_KeepsRepeatedPointsAsZeroLengthSegments()
        {
            // Documented behaviour: filtering is RemoveConsecutiveNearPoints' job, not this one's.
            List<GeoLine2> segments = OnX(0.0, 0.0, 1.0).ToGeoLine2s();

            Assert.Equal(2, segments.Count);
            Assert.Equal(0.0, segments[0].Length, 12);
        }

        [Fact]
        public void ToGeoLine2s_DoesNotMutateTheInput()
        {
            List<GeoPoint2> points = OnX(0.0, 1.0, 2.0);

            points.ToGeoLine2s();

            Assert.Equal(3, points.Count);
        }

        [Fact]
        public void ToGeoLine2s_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ((List<GeoPoint2>)null).ToGeoLine2s());
        }

        #endregion

        #region RemoveConsecutiveNearPoints

        [Fact]
        public void RemoveConsecutiveNearPoints_ReAnchorsInsteadOfSwallowingTheRun()
        {
            // Every neighbour is 0.6 apart, under the tolerance, but the run must not collapse to one
            // point: each survivor becomes the new anchor, so the chain thins rather than vanishes.
            List<GeoPoint2> result = OnX(0.0, 0.6, 1.2, 1.8, 2.4).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(3, result.Count);
            Assert.Equal(0.0, result[0].X, 12);
            Assert.Equal(1.2, result[1].X, 12);
            Assert.Equal(2.4, result[2].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_LeavesNoConsecutivePairWithinTolerance()
        {
            Tolerance tolerance = Tol(1.0);

            List<GeoPoint2> result = OnX(0.0, 0.1, 0.2, 0.3, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(tolerance);

            for (int i = 1; i < result.Count; i++)
            {
                Assert.False(result[i - 1].IsEqualTo(result[i], tolerance));
            }
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DropsTheLastPointWhenItIsTooClose()
        {
            // The end of the chain is not privileged: 5.4 sits within 1.0 of 5.0 and goes, which shortens
            // the chain. Callers that need the original endpoint have to re-append it themselves.
            List<GeoPoint2> result = OnX(0.0, 5.0, 5.4).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(2, result.Count);
            Assert.Equal(5.0, result[result.Count - 1].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_TreatsTheToleranceItselfAsCoincident()
        {
            // IsEqualTo compares with <=, so a gap of exactly EqualPoint is removed, not kept.
            List<GeoPoint2> result = OnX(0.0, 1.0).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Single(result);
            Assert.Equal(0.0, result[0].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_KeepsPointsFartherThanTheTolerance()
        {
            List<GeoPoint2> result = OnX(0.0, 1.0, 2.0).RemoveConsecutiveNearPoints(Tol(0.5));

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_AlwaysKeepsTheFirstPoint()
        {
            List<GeoPoint2> result = OnX(7.0, 7.0, 7.0).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Single(result);
            Assert.Equal(7.0, result[0].X, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RemoveConsecutiveNearPoints_CopiesShortListsThrough(int count)
        {
            var points = new List<GeoPoint2>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint2(i, 0.0));
            }

            List<GeoPoint2> result = points.RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(count, result.Count);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DoesNotMutateTheInput()
        {
            List<GeoPoint2> points = OnX(0.0, 0.1, 0.2);

            List<GeoPoint2> result = points.RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(3, points.Count);
            Assert.Single(result);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => ((List<GeoPoint2>)null).RemoveConsecutiveNearPoints(Tol(1.0)));
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_FeedsToGeoLine2sWithoutZeroLengthEdges()
        {
            Tolerance tolerance = Tol(1.0);

            List<GeoLine2> segments = OnX(0.0, 0.1, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(tolerance)
                .ToGeoLine2s();

            Assert.Equal(2, segments.Count);
            foreach (GeoLine2 segment in segments)
            {
                Assert.True(segment.Length > tolerance.EqualPoint);
            }
        }

        #endregion
    }
}
