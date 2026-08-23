using System;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers point arithmetic and the queries a point forwards to the Core operations.
    /// </summary>
    public class GeoPoint3Tests
    {
        [Fact]
        public void DistanceBetweenTwoPointsIsEuclidean()
        {
            GeoPoint3 a = new GeoPoint3(0.0, 0.0, 0.0);
            GeoPoint3 b = new GeoPoint3(3.0, 4.0, 12.0);

            Assert.Equal(13.0, a.DistanceTo(b), 9);
            Assert.Equal(169.0, a.GetDistanceSquaredTo(b), 9);
        }

        [Fact]
        public void DistanceIsSymmetric()
        {
            GeoPoint3 a = new GeoPoint3(1.0, -2.0, 3.0);
            GeoPoint3 b = new GeoPoint3(-4.0, 5.0, -6.0);

            Assert.Equal(a.DistanceTo(b), b.DistanceTo(a), 12);
        }

        [Fact]
        public void VectorBetweenPointsRunsFromThisToTheOther()
        {
            GeoPoint3 a = new GeoPoint3(1.0, 1.0, 1.0);
            GeoPoint3 b = new GeoPoint3(4.0, 5.0, 6.0);

            Assert.True(a.GetVectorTo(b).IsEqualTo(new GeoVector3(3.0, 4.0, 5.0)));
            Assert.True((b - a).IsEqualTo(new GeoVector3(3.0, 4.0, 5.0)));
        }

        [Fact]
        public void MiddlePointSitsHalfwayAndIsSymmetric()
        {
            GeoPoint3 a = new GeoPoint3(0.0, 0.0, 0.0);
            GeoPoint3 b = new GeoPoint3(2.0, 4.0, 6.0);

            Assert.True(a.GetMiddlePoint(b).IsEqualTo(new GeoPoint3(1.0, 2.0, 3.0)));
            Assert.True(a.GetMiddlePoint(b).IsEqualTo(b.GetMiddlePoint(a)));
        }

        [Fact]
        public void AddingThenSubtractingAVectorReturnsTheSamePoint()
        {
            GeoPoint3 point = new GeoPoint3(1.5, -2.5, 3.5);
            GeoVector3 offset = new GeoVector3(10.0, -20.0, 30.0);

            Assert.True(point.Add(offset).Subtract(offset).IsEqualTo(point));
            Assert.Equal(point.Add(offset), point + offset);
            Assert.Equal(point.Subtract(offset), point - offset);
        }

        [Fact]
        public void DistanceToASegmentClampsToItsEndpoints()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));

            // Beside the middle: the perpendicular distance.
            Assert.Equal(5.0, new GeoPoint3(5.0, 5.0, 0.0).DistanceTo(line), 9);

            // Past the end: measured to the endpoint, not to the infinite line.
            Assert.Equal(5.0, new GeoPoint3(15.0, 0.0, 0.0).DistanceTo(line), 9);
        }

        [Fact]
        public void DistanceToAPlaneIsPerpendicularAndUnsigned()
        {
            GeoPlane3 plane = GeoPlane3.XY;

            Assert.Equal(7.0, new GeoPoint3(100.0, -50.0, 7.0).DistanceTo(plane), 9);
            Assert.Equal(7.0, new GeoPoint3(100.0, -50.0, -7.0).DistanceTo(plane), 9);
        }

        [Fact]
        public void SideOfAPlaneFollowsItsNormal()
        {
            GeoPlane3 plane = GeoPlane3.XY;

            Assert.Equal(PlaneSide.Above, new GeoPoint3(0.0, 0.0, 1.0).GetSideOf(plane));
            Assert.Equal(PlaneSide.Below, new GeoPoint3(0.0, 0.0, -1.0).GetSideOf(plane));
            Assert.Equal(PlaneSide.On, new GeoPoint3(5.0, 5.0, 0.0).GetSideOf(plane));
            Assert.Equal(PlaneSide.Below, new GeoPoint3(0.0, 0.0, 1.0).GetSideOf(plane.Flip()));
        }

        [Fact]
        public void ClosestPointOnASegmentIsClampedToIt()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));

            Assert.True(new GeoPoint3(4.0, 3.0, 0.0).GetClosestPointOnBoundary(line).IsEqualTo(new GeoPoint3(4.0, 0.0, 0.0)));
            Assert.True(new GeoPoint3(-5.0, 3.0, 0.0).GetClosestPointOnBoundary(line).IsEqualTo(new GeoPoint3(0.0, 0.0, 0.0)));
        }

        [Fact]
        public void ClosestPointOnARayIsClampedToItsOrigin()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);

            Assert.True(new GeoPoint3(4.0, 3.0, 0.0).GetClosestPointOnBoundary(ray).IsEqualTo(new GeoPoint3(4.0, 0.0, 0.0)));
            Assert.True(new GeoPoint3(-4.0, 3.0, 0.0).GetClosestPointOnBoundary(ray).IsEqualTo(GeoPoint3.Origin));
        }

        [Fact]
        public void PointOnASegmentIsRecognisedAndOneBeyondItIsNot()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));

            Assert.True(new GeoPoint3(5.0, 0.0, 0.0).IsPointOn(line));
            Assert.True(new GeoPoint3(10.0, 0.0, 0.0).IsPointOn(line));
            Assert.False(new GeoPoint3(10.5, 0.0, 0.0).IsPointOn(line));
            Assert.False(new GeoPoint3(5.0, 0.5, 0.0).IsPointOn(line));
        }

        [Fact]
        public void PointBehindARayOriginIsNotOnTheRay()
        {
            GeoRay3 ray = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);

            Assert.True(new GeoPoint3(1000.0, 0.0, 0.0).IsPointOn(ray));
            Assert.False(new GeoPoint3(-1.0, 0.0, 0.0).IsPointOn(ray));
        }

        [Fact]
        public void ToVectorReadsThePointAsAnOffsetFromTheWorldOrigin()
        {
            GeoPoint3 point = new GeoPoint3(1.0, 2.0, 3.0);

            Assert.True(point.ToVector().IsEqualTo(new GeoVector3(1.0, 2.0, 3.0)));
            Assert.True(GeoPoint3.Origin.Add(point.ToVector()).IsEqualTo(point));
        }

        [Fact]
        public void DefaultValueIsTheOrigin()
        {
            Assert.Equal(GeoPoint3.Origin, default(GeoPoint3));
        }

        [Fact]
        public void EqualHashCodesFollowEqualValues()
        {
            GeoPoint3 a = new GeoPoint3(1.5, -2.5, 3.5);
            GeoPoint3 b = new GeoPoint3(1.5, -2.5, 3.5);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a == b);
            Assert.False(a != b);
        }
    }
}
