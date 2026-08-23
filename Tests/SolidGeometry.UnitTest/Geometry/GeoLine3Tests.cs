using System;
using SolidGeometry;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the line segment: its measurements, its parametrization and how it meets other shapes.
    /// </summary>
    public class GeoLine3Tests
    {
        private static readonly GeoLine3 UnitX = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));

        [Fact]
        public void LengthAndMidPointFollowTheEndpoints()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(2.0, 3.0, 6.0));

            Assert.Equal(7.0, line.Length, 9);
            Assert.Equal(49.0, line.LengthSquared, 9);
            Assert.True(line.MidPoint.IsEqualTo(new GeoPoint3(1.0, 1.5, 3.0)));
        }

        [Fact]
        public void DirectionCarriesTheLengthOfTheSegment()
        {
            Assert.True(UnitX.Direction.IsEqualTo(new GeoVector3(10.0, 0.0, 0.0)));
            Assert.Equal(UnitX.Length, UnitX.Direction.Length, 9);
        }

        [Fact]
        public void ReverseSwapsTheEndpointsAndKeepsTheLength()
        {
            GeoLine3 reversed = UnitX.Reverse();

            Assert.True(reversed.StartPoint.IsEqualTo(UnitX.EndPoint));
            Assert.True(reversed.EndPoint.IsEqualTo(UnitX.StartPoint));
            Assert.Equal(UnitX.Length, reversed.Length, 9);
        }

        [Fact]
        public void ParameterZeroIsTheStartAndOneIsTheEnd()
        {
            Assert.True(UnitX.GetPointAtParameter(0.0).IsEqualTo(UnitX.StartPoint));
            Assert.True(UnitX.GetPointAtParameter(1.0).IsEqualTo(UnitX.EndPoint));
            Assert.True(UnitX.GetPointAtParameter(0.5).IsEqualTo(UnitX.MidPoint));
        }

        [Fact]
        public void ParametersOutsideTheRangeExtrapolateAlongTheCarrierLine()
        {
            Assert.True(UnitX.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint3(20.0, 0.0, 0.0)));
            Assert.True(UnitX.GetPointAtParameter(-0.5).IsEqualTo(new GeoPoint3(-5.0, 0.0, 0.0)));
        }

        [Fact]
        public void ParameterAndDistanceAreProportional()
        {
            Assert.Equal(2.5, UnitX.GetDistanceAtParameter(0.25), 9);
            Assert.Equal(0.25, UnitX.GetParameterAtDistance(2.5), 9);
            Assert.True(UnitX.GetPointAtDistance(2.5).IsEqualTo(new GeoPoint3(2.5, 0.0, 0.0)));
        }

        [Fact]
        public void ParameterAtAPointProjectsOntoTheCarrierLine()
        {
            Assert.Equal(0.4, UnitX.GetParameterAtPoint(new GeoPoint3(4.0, 7.0, -2.0)), 9);
            Assert.Equal(4.0, UnitX.GetDistanceAtPoint(new GeoPoint3(4.0, 7.0, -2.0)), 9);
        }

        [Fact]
        public void ADegenerateSegmentIsRecognisedAndReportsParameterZero()
        {
            GeoLine3 dot = new GeoLine3(new GeoPoint3(3.0, 3.0, 3.0), new GeoPoint3(3.0, 3.0, 3.0));

            Assert.True(dot.IsDegenerate());
            Assert.Equal(0.0, dot.GetParameterAtPoint(new GeoPoint3(9.0, 9.0, 9.0)), 9);
            Assert.False(UnitX.IsDegenerate());
        }

        [Fact]
        public void DistanceBetweenTwoSkewSegmentsIsTheGapBetweenThem()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 alongY = new GeoLine3(new GeoPoint3(0.0, -5.0, 4.0), new GeoPoint3(0.0, 5.0, 4.0));

            Assert.Equal(4.0, alongX.DistanceTo(alongY), 9);
            Assert.Equal(4.0, alongY.DistanceTo(alongX), 9);
        }

        [Fact]
        public void DistanceBetweenParallelSegmentsIsTheirSeparation()
        {
            GeoLine3 lower = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));
            GeoLine3 upper = new GeoLine3(new GeoPoint3(0.0, 3.0, 0.0), new GeoPoint3(10.0, 3.0, 0.0));

            Assert.Equal(3.0, lower.DistanceTo(upper), 9);
        }

        [Fact]
        public void ClosestConnectingSegmentStartsOnOneAndEndsOnTheOther()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 alongY = new GeoLine3(new GeoPoint3(0.0, -5.0, 4.0), new GeoPoint3(0.0, 5.0, 4.0));

            GeoLine3 bridge = alongX.GetClosestOnBoundary(alongY);

            Assert.True(bridge.StartPoint.IsPointOn(alongX));
            Assert.True(bridge.EndPoint.IsPointOn(alongY));
            Assert.Equal(4.0, bridge.Length, 9);
        }

        [Fact]
        public void CrossingSegmentsMeetAtASinglePoint()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 alongY = new GeoLine3(new GeoPoint3(0.0, -5.0, 0.0), new GeoPoint3(0.0, 5.0, 0.0));

            Assert.True(alongX.TryIntersectWith(alongY, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(GeoPoint3.Origin));
        }

        [Fact]
        public void SkewSegmentsDoNotMeet()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 alongY = new GeoLine3(new GeoPoint3(0.0, -5.0, 4.0), new GeoPoint3(0.0, 5.0, 4.0));

            Assert.False(alongX.TryIntersectWith(alongY, out _));
        }

        [Fact]
        public void CollinearOverlappingSegmentsAreRefusedBecauseThereIsNoSinglePoint()
        {
            GeoLine3 first = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));
            GeoLine3 second = new GeoLine3(new GeoPoint3(5.0, 0.0, 0.0), new GeoPoint3(15.0, 0.0, 0.0));

            Assert.False(first.TryIntersectWith(second, out _));
        }

        [Fact]
        public void SegmentCrossingAPlaneGivesThePointWhereItPasses()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, -5.0), new GeoPoint3(0.0, 0.0, 5.0));

            Assert.True(line.TryIntersectWith(GeoPlane3.XY, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(GeoPoint3.Origin));
        }

        [Fact]
        public void SegmentStoppingShortOfAPlaneDoesNotCrossIt()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 5.0), new GeoPoint3(0.0, 0.0, 1.0));

            Assert.False(line.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void SegmentLyingInAPlaneIsRefusedBecauseEveryPointWouldQualify()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(10.0, 0.0, 0.0));

            Assert.False(line.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void ParallelAndPerpendicularReadTheDirectionsOnly()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(1.0, 0.0, 0.0));
            GeoLine3 alongXElsewhere = new GeoLine3(new GeoPoint3(0.0, 9.0, 9.0), new GeoPoint3(-1.0, 9.0, 9.0));
            GeoLine3 alongY = new GeoLine3(new GeoPoint3(0.0, 0.0, 5.0), new GeoPoint3(0.0, 1.0, 5.0));

            Assert.True(alongX.IsParallelTo(alongXElsewhere));
            Assert.True(alongX.IsPerpendicularTo(alongY));
        }

        [Fact]
        public void CoplanarityHoldsForIntersectingAndParallelSegmentsButNotForSkewOnes()
        {
            GeoLine3 alongX = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 0.0, 0.0));
            GeoLine3 crossing = new GeoLine3(new GeoPoint3(0.0, -5.0, 0.0), new GeoPoint3(0.0, 5.0, 0.0));
            GeoLine3 parallel = new GeoLine3(new GeoPoint3(-5.0, 3.0, 0.0), new GeoPoint3(5.0, 3.0, 0.0));
            GeoLine3 skew = new GeoLine3(new GeoPoint3(0.0, -5.0, 4.0), new GeoPoint3(0.0, 5.0, 4.0));

            Assert.True(alongX.IsCoplanarWith(crossing));
            Assert.True(alongX.IsCoplanarWith(parallel));
            Assert.False(alongX.IsCoplanarWith(skew));
        }

        [Fact]
        public void ToleranceEqualityIgnoresWhichWayRoundTheEndpointsAreGiven()
        {
            Assert.True(UnitX.IsEqualTo(UnitX.Reverse()));
            Assert.False(UnitX.Equals(UnitX.Reverse()));
        }

        [Fact]
        public void EqualHashCodesFollowEqualValues()
        {
            GeoLine3 a = new GeoLine3(0.0, 1.0, 2.0, 3.0, 4.0, 5.0);
            GeoLine3 b = new GeoLine3(new GeoPoint3(0.0, 1.0, 2.0), new GeoPoint3(3.0, 4.0, 5.0));

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }
    }
}
