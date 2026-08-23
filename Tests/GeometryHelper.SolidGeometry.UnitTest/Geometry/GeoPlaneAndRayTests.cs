using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the plane and the ray, the two unbounded shapes in the library.
    /// </summary>
    public class GeoPlaneAndRayTests
    {
        #region Plane

        [Fact]
        public void PlaneNormalIsNormalizedOnConstruction()
        {
            GeoPlane3 plane = new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0.0, 0.0, 7.0));

            Assert.True(plane.Normal.IsUnitLength());
            Assert.True(plane.Normal.IsEqualTo(GeoVector3.ZAxis));
        }

        [Fact]
        public void PlaneRefusesADegenerateNormal()
        {
            Assert.Throws<ArgumentException>(() => new GeoPlane3(GeoPoint3.Origin, GeoVector3.Zero));
        }

        [Fact]
        public void PlaneThroughThreePointsFollowsTheRightHandRule()
        {
            GeoPlane3 plane = GeoPlane3.FromThreePoints(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(0.0, 1.0, 0.0));

            Assert.True(plane.Normal.IsEqualTo(GeoVector3.ZAxis));
        }

        [Fact]
        public void PlaneThroughCollinearPointsIsRefused()
        {
            Assert.Throws<ArgumentException>(() => GeoPlane3.FromThreePoints(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 1.0, 1.0),
                new GeoPoint3(2.0, 2.0, 2.0)));
        }

        [Fact]
        public void SignedDistanceIsPositiveOnTheNormalSide()
        {
            GeoPlane3 plane = GeoPlane3.XY;

            Assert.Equal(5.0, plane.SignedDistanceTo(new GeoPoint3(9.0, -9.0, 5.0)), 9);
            Assert.Equal(-5.0, plane.SignedDistanceTo(new GeoPoint3(9.0, -9.0, -5.0)), 9);
        }

        [Fact]
        public void FlipReversesTheSidesButKeepsTheSurface()
        {
            GeoPlane3 plane = GeoPlane3.XY;
            GeoPlane3 flipped = plane.Flip();

            Assert.Equal(-plane.SignedDistanceTo(new GeoPoint3(0.0, 0.0, 3.0)), flipped.SignedDistanceTo(new GeoPoint3(0.0, 0.0, 3.0)), 9);
            Assert.True(flipped.IsParallelTo(plane));
            Assert.False(flipped.IsEqualTo(plane));
        }

        [Fact]
        public void OffsetMovesThePlaneAlongItsNormal()
        {
            GeoPlane3 raised = GeoPlane3.XY.Offset(4.0);

            Assert.Equal(0.0, raised.SignedDistanceTo(new GeoPoint3(1.0, 1.0, 4.0)), 9);
            Assert.Equal(4.0, raised.DistanceFromWorldOrigin, 9);
        }

        [Fact]
        public void ProjectionOntoAPlaneDropsTheNormalComponent()
        {
            GeoPlane3 plane = GeoPlane3.XY;

            Assert.True(plane.Project(new GeoPoint3(2.0, 3.0, 7.0)).IsEqualTo(new GeoPoint3(2.0, 3.0, 0.0)));
            Assert.True(plane.Project(new GeoVector3(2.0, 3.0, 7.0)).IsEqualTo(new GeoVector3(2.0, 3.0, 0.0)));
        }

        [Fact]
        public void PlaneAxesAreOrthonormalAndRightHandedWithTheNormal()
        {
            GeoPlane3[] planes =
            {
                GeoPlane3.XY,
                GeoPlane3.XZ,
                GeoPlane3.YZ,
                new GeoPlane3(new GeoPoint3(1.0, 2.0, 3.0), new GeoVector3(1.0, 1.0, 1.0))
            };

            foreach (GeoPlane3 plane in planes)
            {
                plane.GetAxes(out GeoVector3 u, out GeoVector3 v);

                Assert.True(u.IsUnitLength());
                Assert.True(v.IsUnitLength());
                Assert.True(u.IsPerpendicularTo(v));
                Assert.True(u.CrossProduct(v).IsEqualTo(plane.Normal));
            }
        }

        [Fact]
        public void ContainsAllUsesThePlanarThreshold()
        {
            GeoPlane3 plane = GeoPlane3.XY;
            Tolerance loose = new Tolerance(1E-4, 1E-4, 0.01, 0.5);

            GeoPoint3[] slightlyOff =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.2),
                new GeoPoint3(1.0, 1.0, -0.2)
            };

            Assert.True(plane.ContainsAll(slightlyOff, loose));
            Assert.False(plane.ContainsAll(slightlyOff));
        }

        [Fact]
        public void TwoPlanesMeetInALineAndParallelOnesDoNot()
        {
            Assert.True(GeoPlane3.XY.TryIntersectWith(GeoPlane3.YZ, out GeoRay3 line));
            Assert.True(line.Direction.IsParallelTo(GeoVector3.YAxis));
            Assert.True(line.Origin.IsPointOn(GeoPlane3.XY));
            Assert.True(line.Origin.IsPointOn(GeoPlane3.YZ));

            Assert.False(GeoPlane3.XY.TryIntersectWith(GeoPlane3.XY.Offset(5.0), out _));
            Assert.False(GeoPlane3.XY.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void PlaneEqualityIgnoresWhereTheOriginSitsOnTheSurface()
        {
            GeoPlane3 atOrigin = GeoPlane3.XY;
            GeoPlane3 elsewhere = new GeoPlane3(new GeoPoint3(100.0, -50.0, 0.0), GeoVector3.ZAxis);

            Assert.True(atOrigin.IsEqualTo(elsewhere));
            Assert.False(atOrigin.Equals(elsewhere));
        }

        #endregion

        #region Ray

        [Fact]
        public void RayDirectionIsNormalizedOnConstruction()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, new GeoVector3(0.0, 9.0, 0.0));

            Assert.True(ray.Direction.IsEqualTo(GeoVector3.YAxis));
        }

        [Fact]
        public void RayRefusesADegenerateDirection()
        {
            Assert.Throws<ArgumentException>(() => new GeoRay3(GeoPoint3.Origin, GeoVector3.Zero));
            Assert.Throws<ArgumentException>(() => new GeoRay3(GeoPoint3.Origin, GeoPoint3.Origin));
        }

        [Fact]
        public void RayThroughAPointAimsAtThatPoint()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, new GeoPoint3(0.0, 0.0, 10.0));

            Assert.True(ray.Direction.IsEqualTo(GeoVector3.ZAxis));
            Assert.True(new GeoPoint3(0.0, 0.0, 10.0).IsPointOn(ray));
        }

        [Fact]
        public void PointAtDistanceMeasuresTrueArcLengthBecauseTheDirectionIsNormalized()
        {
            GeoRay3 ray = new GeoRay3(new GeoPoint3(1.0, 0.0, 0.0), new GeoVector3(3.0, 4.0, 0.0));

            Assert.True(ray.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint3(4.0, 4.0, 0.0)));
            Assert.Equal(5.0, ray.GetDistanceAtPoint(new GeoPoint3(4.0, 4.0, 0.0)), 9);
        }

        [Fact]
        public void NegativeDistanceExtrapolatesBehindTheOriginAndOffTheRay()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);
            GeoPoint3 behind = ray.GetPointAtDistance(-5.0);

            Assert.True(behind.IsEqualTo(new GeoPoint3(-5.0, 0.0, 0.0)));
            Assert.False(behind.IsPointOn(ray));
        }

        [Fact]
        public void ToLineSamplesTheRayAndRefusesANegativeReach()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);

            Assert.Equal(7.0, ray.ToLine(7.0).Length, 9);
            Assert.Throws<ArgumentOutOfRangeException>(() => ray.ToLine(-1.0));
        }

        [Fact]
        public void RayCrossesAPlaneAheadOfItButNotBehindIt()
        {
            GeoRay3 upward = new GeoRay3(new GeoPoint3(0.0, 0.0, -5.0), GeoVector3.ZAxis);
            GeoRay3 downward = new GeoRay3(new GeoPoint3(0.0, 0.0, -5.0), GeoVector3.ZAxis.Negate());

            Assert.True(upward.TryIntersectWith(GeoPlane3.XY, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(GeoPoint3.Origin));
            Assert.False(downward.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void RayParallelToAPlaneNeverMeetsIt()
        {
            GeoRay3 ray = new GeoRay3(new GeoPoint3(0.0, 0.0, 5.0), GeoVector3.XAxis);

            Assert.True(ray.IsParallelTo(GeoPlane3.XY));
            Assert.False(ray.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void ReverseTurnsTheRayAroundItsOwnOrigin()
        {
            GeoRay3 ray = new GeoRay3(new GeoPoint3(1.0, 2.0, 3.0), GeoVector3.XAxis);
            GeoRay3 reversed = ray.Reverse();

            Assert.True(reversed.Origin.IsEqualTo(ray.Origin));
            Assert.True(reversed.Direction.IsEqualTo(ray.Direction.Negate()));
        }

        #endregion
    }
}
