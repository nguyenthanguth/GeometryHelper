using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Extension;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Extension
{
    public class EnumerableExtensionTests
    {
        private static Tolerance Tol(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

        private static List<GeoPoint3> OnX(params double[] xs)
        {
            var points = new List<GeoPoint3>();
            foreach (double x in xs)
            {
                points.Add(new GeoPoint3(x, 0.0, 0.0));
            }

            return points;
        }

        #region ToGeoLine3s

        [Fact]
        public void ToGeoLine3s_ChainsConsecutivePairs()
        {
            List<GeoLine3> segments = OnX(0.0, 1.0, 3.0).ToGeoLine3s();

            Assert.Equal(2, segments.Count);
            Assert.Equal(new GeoPoint3(0.0, 0.0, 0.0), segments[0].StartPoint);
            Assert.Equal(new GeoPoint3(1.0, 0.0, 0.0), segments[0].EndPoint);
            Assert.Equal(new GeoPoint3(1.0, 0.0, 0.0), segments[1].StartPoint);
            Assert.Equal(new GeoPoint3(3.0, 0.0, 0.0), segments[1].EndPoint);
        }

        [Fact]
        public void ToGeoLine3s_LeavesTheChainOpen()
        {
            // First and last vertex coincide, so the ring is explicitly closed by the caller. The helper
            // must not add a further closing segment on top of that.
            List<GeoLine3> segments = OnX(0.0, 1.0, 2.0, 0.0).ToGeoLine3s();

            Assert.Equal(3, segments.Count);
            Assert.Equal(segments[0].StartPoint, segments[segments.Count - 1].EndPoint);
        }

        [Fact]
        public void ToGeoLine3s_KeepsTheThirdDimension()
        {
            var points = new List<GeoPoint3>
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0, 0.0, 4.0),
            };

            List<GeoLine3> segments = points.ToGeoLine3s();

            Assert.Single(segments);
            Assert.Equal(4.0, segments[0].Length, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ToGeoLine3s_YieldsNothingBelowTwoPoints(int count)
        {
            var points = new List<GeoPoint3>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint3(i, 0.0, 0.0));
            }

            Assert.Empty(points.ToGeoLine3s());
        }

        [Fact]
        public void ToGeoLine3s_KeepsRepeatedPointsAsZeroLengthSegments()
        {
            // Documented behaviour: filtering belongs to RemoveConsecutiveNearPoints, not here.
            List<GeoLine3> segments = OnX(0.0, 0.0, 1.0).ToGeoLine3s();

            Assert.Equal(2, segments.Count);
            Assert.Equal(0.0, segments[0].Length, 12);
        }

        [Fact]
        public void ToGeoLine3s_DoesNotMutateTheInput()
        {
            List<GeoPoint3> points = OnX(0.0, 1.0, 2.0);

            points.ToGeoLine3s();

            Assert.Equal(3, points.Count);
        }

        [Fact]
        public void ToGeoLine3s_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ((List<GeoPoint3>)null).ToGeoLine3s());
        }

        #endregion

        #region RemoveConsecutiveNearPoints

        [Fact]
        public void RemoveConsecutiveNearPoints_ReAnchorsInsteadOfSwallowingTheRun()
        {
            // Every neighbour is 0.6 apart, under the tolerance, but the run must not collapse to one
            // point: each survivor becomes the new anchor, so the chain thins rather than vanishes.
            List<GeoPoint3> result = OnX(0.0, 0.6, 1.2, 1.8, 2.4).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(3, result.Count);
            Assert.Equal(0.0, result[0].X, 12);
            Assert.Equal(1.2, result[1].X, 12);
            Assert.Equal(2.4, result[2].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_LeavesNoConsecutivePairWithinTolerance()
        {
            Tolerance tolerance = Tol(1.0);

            List<GeoPoint3> result = OnX(0.0, 0.1, 0.2, 0.3, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(tolerance);

            for (int i = 1; i < result.Count; i++)
            {
                Assert.False(result[i - 1].IsEqualTo(result[i], tolerance));
            }
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_MeasuresInThreeDimensions()
        {
            // The two points share X and Y and differ only in Z, so a purely planar comparison would
            // wrongly fuse them.
            var points = new List<GeoPoint3>
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0, 0.0, 5.0),
            };

            Assert.Equal(2, points.RemoveConsecutiveNearPoints(Tol(1.0)).Count);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DropsTheLastPointWhenItIsTooClose()
        {
            // The end of the chain is not privileged: 5.4 sits within 1.0 of 5.0 and goes, which shortens
            // the chain. Callers that need the original endpoint have to re-append it themselves.
            List<GeoPoint3> result = OnX(0.0, 5.0, 5.4).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(2, result.Count);
            Assert.Equal(5.0, result[result.Count - 1].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_TreatsTheToleranceItselfAsCoincident()
        {
            // IsEqualTo compares with <=, so a gap of exactly EqualPoint is removed, not kept.
            List<GeoPoint3> result = OnX(0.0, 1.0).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Single(result);
            Assert.Equal(0.0, result[0].X, 12);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_KeepsPointsFartherThanTheTolerance()
        {
            List<GeoPoint3> result = OnX(0.0, 1.0, 2.0).RemoveConsecutiveNearPoints(Tol(0.5));

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_AlwaysKeepsTheFirstPoint()
        {
            List<GeoPoint3> result = OnX(7.0, 7.0, 7.0).RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Single(result);
            Assert.Equal(7.0, result[0].X, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RemoveConsecutiveNearPoints_CopiesShortListsThrough(int count)
        {
            var points = new List<GeoPoint3>();
            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint3(i, 0.0, 0.0));
            }

            List<GeoPoint3> result = points.RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(count, result.Count);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_DoesNotMutateTheInput()
        {
            List<GeoPoint3> points = OnX(0.0, 0.1, 0.2);

            List<GeoPoint3> result = points.RemoveConsecutiveNearPoints(Tol(1.0));

            Assert.Equal(3, points.Count);
            Assert.Single(result);
            Assert.NotSame(points, result);
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => ((List<GeoPoint3>)null).RemoveConsecutiveNearPoints(Tol(1.0)));
        }

        [Fact]
        public void RemoveConsecutiveNearPoints_FeedsToGeoLine3sWithoutZeroLengthEdges()
        {
            Tolerance tolerance = Tol(1.0);

            List<GeoLine3> segments = OnX(0.0, 0.1, 5.0, 5.05, 9.0)
                .RemoveConsecutiveNearPoints(tolerance)
                .ToGeoLine3s();

            Assert.Equal(2, segments.Count);
            foreach (GeoLine3 segment in segments)
            {
                Assert.True(segment.Length > tolerance.EqualPoint);
            }
        }

        #endregion
    }
}
