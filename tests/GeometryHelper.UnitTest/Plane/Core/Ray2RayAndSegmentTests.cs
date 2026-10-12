using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ray in the plane against a point (D12), another ray (D11) and a segment (D9). Every expected number is worked
    /// out by hand from the rule the spec gives: the clamped projection for a point; for two rays the crossing where both
    /// reach it, within the point tolerance behind either origin, and otherwise the nearer of the two origins brought onto
    /// the other ray; for a segment, the answer of the segment from the origin out past the segment's reach.
    /// </summary>
    /// <remarks>
    /// The point tolerance is a thousandth. Offsets of 0.0005 and 0.002 sit either side of it. The angle tolerance is a
    /// degree, and the near-parallel rays here are at half a degree and two degrees, clear of it either way.
    /// </remarks>
    public class Ray2RayAndSegmentTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoRay2 AlongX() => new GeoRay2(P(0, 0), GeoVector2.XAxis);

        private static void AssertNear(GeoPoint2 expected, GeoPoint2 actual, double within = 1E-9)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertLine(GeoLine2 actual, GeoPoint2 start, GeoPoint2 end, double within = 1E-9)
        {
            AssertNear(start, actual.StartPoint, within);
            AssertNear(end, actual.EndPoint, within);
        }

        #region A point (D12)

        [Fact]
        public void APointBesideTheRay_IsMeasuredToItsFoot()
        {
            // (5, 3) has its foot at (5, 0): 3 away, and the line runs from the foot to the point.
            GeoRay2 ray = AlongX();

            Assert.Equal(3.0, ray.DistanceTo(P(5, 3)), 12);
            AssertLine(ray.GetShortestLineTo(P(5, 3), Strict), P(5, 0), P(5, 3));
        }

        [Fact]
        public void APointBehindTheOrigin_IsMeasuredToTheOrigin()
        {
            // (-4, 3) projects four behind the origin, so the clamped foot is the origin and the distance is 5.
            GeoRay2 ray = AlongX();

            Assert.Equal(5.0, ray.DistanceTo(P(-4, 3)), 12);
            AssertLine(ray.GetShortestLineTo(P(-4, 3), Strict), P(0, 0), P(-4, 3));
        }

        [Fact]
        public void APointFiveTenThousandthsBehindTheOrigin_IsThatFarAway_ThoughItIsOnTheRay()
        {
            // The distance is not rounded to the tolerance: 0.0005, though IsPointOn says it is on the ray.
            GeoRay2 ray = AlongX();

            Assert.Equal(0.0005, ray.DistanceTo(P(-0.0005, 0)), 12);
            Assert.True(ray.IsPointOn(P(-0.0005, 0), Strict));
        }

        [Fact]
        public void APointOnTheRay_IsNoughtAwayAndItsLineHasNoLength()
        {
            GeoRay2 ray = AlongX();

            Assert.Equal(0.0, ray.DistanceTo(P(7, 0)));
            AssertLine(ray.GetShortestLineTo(P(7, 0), Strict), P(7, 0), P(7, 0));
        }

        [Fact]
        public void ThePointAsksTheSameDistance_AndItsLineStartsAtThePoint()
        {
            // GeoPoint2.GetShortestLineTo(GeoLine2) leaves the point; so does the ray's counterpart.
            GeoRay2 ray = AlongX();

            Assert.Equal(ray.DistanceTo(P(5, 3)), P(5, 3).DistanceTo(ray), 12);
            Assert.Equal(ray.DistanceTo(P(-4, 3)), P(-4, 3).DistanceTo(ray), 12);
            AssertLine(P(5, 3).GetShortestLineTo(ray, Strict), P(5, 3), P(5, 0));
            AssertLine(P(-4, 3).GetShortestLineTo(ray, Strict), P(-4, 3), P(0, 0));
        }

        #endregion

        #region Two rays (D11)

        [Fact]
        public void TwoRaysCrossingSquare_MeetAtFiftyNought()
        {
            // Along X from the origin, and along Y from (50, -50): they cross at (50, 0), 50 along each.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(50, -50), GeoVector2.YAxis);

            Assert.True(ray.CollidesWith(other, Strict));
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.True(ray.TryIntersectWith(other, out GeoPoint2 crossing, Strict));
            AssertNear(P(50, 0), crossing);
            AssertLine(ray.GetShortestLineTo(other, Strict), P(50, 0), P(50, 0));
        }

        [Fact]
        public void ARayAtFortyFiveDegrees_CrossesAt50Nought()
        {
            // From (100, -50) along (-1, 1): it reaches y = 0 at (50, 0), 50√2 along it.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(100, -50), new GeoVector2(-1, 1));

            Assert.True(ray.TryIntersectWith(other, out GeoPoint2 crossing, Strict));
            AssertNear(P(50, 0), crossing);
            Assert.True(other.TryIntersectWith(ray, out GeoPoint2 back, Strict));
            AssertNear(P(50, 0), back);
        }

        [Fact]
        public void ARayPointingAwayAboveTheFirst_IsThirtyAway_FromItsOriginDown()
        {
            // From (50, 30) along Y the carriers cross 30 behind its origin. The candidates are its origin brought onto the
            // first ray, (50, 0) at 30, and the first's origin brought onto it, its own origin at √3400 = 58.3; so 30.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(50, 30), GeoVector2.YAxis);

            Assert.False(ray.CollidesWith(other, Strict));
            Assert.Equal(30.0, ray.DistanceTo(other, Strict), 12);
            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            AssertLine(ray.GetShortestLineTo(other, Strict), P(50, 0), P(50, 30));
            AssertLine(other.GetShortestLineTo(ray, Strict), P(50, 30), P(50, 0));
            Assert.Equal(30.0, other.DistanceTo(ray, Strict), 12);
        }

        [Fact]
        public void ARayDivergingUpAndRight_IsTenAway_FromItsOrigin()
        {
            // From (20, 10) along (1, 1) its carrier meets y = 0 at (10, 0), 10√2 behind it. Its origin is 10 above (20, 0);
            // the first ray's origin brought onto it lands on its origin, √500 = 22.4 away.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(20, 10), new GeoVector2(1, 1));

            Assert.Equal(10.0, ray.DistanceTo(other, Strict), 12);
            AssertLine(ray.GetShortestLineTo(other, Strict), P(20, 0), P(20, 10));
            AssertLine(other.GetShortestLineTo(ray, Strict), P(20, 10), P(20, 0));
        }

        [Fact]
        public void ARayCrossingTheOrigin_MeetsItThere()
        {
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(0, -5), GeoVector2.YAxis);

            Assert.True(ray.TryIntersectWith(other, out GeoPoint2 crossing, Strict));
            AssertNear(P(0, 0), crossing);
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.True(ray.CollidesWith(other, Strict));
        }

        [Fact]
        public void ACrossingFiveTenThousandthsBehindTheOrigin_Counts_AndIsPlacedOnTheOrigin()
        {
            // The carriers cross at (-0.0005, 0), within the tolerance behind the first origin: the crossing is given,
            // brought onto the first ray at t = 0.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(-0.0005, -5), GeoVector2.YAxis);

            Assert.True(ray.TryIntersectWith(other, out GeoPoint2 crossing, Strict));
            Assert.Equal(P(0, 0), crossing);
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.True(ray.CollidesWith(other, Strict));
        }

        [Fact]
        public void ACrossingTwoThousandthsBehindTheOrigin_DoesNotCount_AndTheRaysAreTwoThousandthsApart()
        {
            // The candidates: the other's origin (-0.002, -5) onto the first ray is the origin, √(25.000004) away; the first
            // origin onto the other is (-0.002, 0), 0.002 away. So 0.002, more than the tolerance: no contact.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(-0.002, -5), GeoVector2.YAxis);

            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            Assert.Equal(0.002, ray.DistanceTo(other, Strict), 12);
            Assert.False(ray.CollidesWith(other, Strict));
            AssertLine(ray.GetShortestLineTo(other, Strict), P(0, 0), P(-0.002, 0));
        }

        [Fact]
        public void ACrossingBehindTheOtherOrigin_DoesNotCount()
        {
            // From (50, 5) along Y the carriers cross 5 behind its origin: 5 apart, from (50, 0) up to its origin.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(50, 5), GeoVector2.YAxis);

            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            Assert.Equal(5.0, ray.DistanceTo(other, Strict), 12);
        }

        [Fact]
        public void TwoRaysSideBySide_AreFiveApart_AndDoNotCross()
        {
            // From (10, 5) along X: its origin is 5 above (10, 0); the first origin onto it is its origin, √125 away.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(10, 5), GeoVector2.XAxis);

            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            Assert.False(ray.CollidesWith(other, Strict));
            Assert.Equal(5.0, ray.DistanceTo(other, Strict), 12);
            AssertLine(ray.GetShortestLineTo(other, Strict), P(10, 0), P(10, 5));
        }

        [Fact]
        public void TwoRaysAlongOneLineFacingEachOther_Touch_ButNameNoCrossing()
        {
            // From 0 along X and from 100 back along -X they share the stretch between: nought apart, touching, and
            // parallel, so TryIntersectWith refuses them, as Intersection2 refuses parallel members.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(100, 0), -GeoVector2.XAxis);

            Assert.True(ray.CollidesWith(other, Strict));
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            Assert.Equal(0.0, ray.GetShortestLineTo(other, Strict).Length);
        }

        [Fact]
        public void TwoRaysAlongOneLineFacingAway_AreTheGapBetweenTheirOriginsApart()
        {
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(-10, 0), -GeoVector2.XAxis);

            Assert.False(ray.CollidesWith(other, Strict));
            Assert.Equal(10.0, ray.DistanceTo(other, Strict), 12);
            AssertLine(ray.GetShortestLineTo(other, Strict), P(0, 0), P(-10, 0));
        }

        [Fact]
        public void TwoRaysOneAheadOfTheOtherAlongOneLine_Touch()
        {
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(50, 0), GeoVector2.XAxis);

            Assert.True(ray.CollidesWith(other, Strict));
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.False(ray.TryIntersectWith(other, out _, Strict));
        }

        [Fact]
        public void ATieBetweenTheTwoCandidates_GoesToTheOtherOriginBroughtOntoThisRay()
        {
            // Along X from the origin, and back along -X from (10, 10): the other's origin onto this ray is (10, 0), 10
            // away; this origin onto the other is (0, 10), 10 away. A tie goes to the first: the line from (10, 0) up. Asked
            // the other way round, the first candidate is the first origin brought onto the other ray: (0, 10) down.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(10, 10), -GeoVector2.XAxis);

            Assert.Equal(10.0, ray.DistanceTo(other, Strict), 12);
            AssertLine(ray.GetShortestLineTo(other, Strict), P(10, 0), P(10, 10));
            AssertLine(other.GetShortestLineTo(ray, Strict), P(0, 10), P(0, 0));
        }

        [Fact]
        public void ARayAtHalfADegreeCrossingAThousandAlong_IsNoughtAway_ButIsParallelForTheCrossingPoint()
        {
            // From (0, 5) along (1, -0.005) it reaches y = 0 at (1000, 0). The sine 0.005 is below sin 1° = 0.0175, so
            // TryIntersectWith calls them parallel by angle (neither has a length), yet they cross: nought apart, touching,
            // and the shortest line is the crossing itself.
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(0, 5), new GeoVector2(1, -0.005));

            Assert.False(ray.TryIntersectWith(other, out _, Strict));
            Assert.Equal(0.0, ray.DistanceTo(other, Strict));
            Assert.True(ray.CollidesWith(other, Strict));
            AssertLine(ray.GetShortestLineTo(other, Strict), P(1000, 0), P(1000, 0), 1E-6);
        }

        [Fact]
        public void ARayAtTwoDegreesCrossingSoonAfter_IsGivenItsCrossingPoint()
        {
            // From (0, 5) at two degrees below X it reaches y = 0 at 5 / tan 2° = 143.18 along X.
            double two = Math.PI / 90;
            GeoRay2 ray = AlongX();
            var other = new GeoRay2(P(0, 5), new GeoVector2(Math.Cos(two), -Math.Sin(two)));

            Assert.True(ray.TryIntersectWith(other, out GeoPoint2 crossing, Strict));
            AssertNear(P(5 / Math.Tan(two), 0), crossing, 1E-9);
        }

        [Fact]
        public void TheFormsWithoutATolerance_AnswerAsTheGlobalToleranceDoes()
        {
            GeoRay2 ray = AlongX();

            foreach (GeoRay2 other in new[] { new GeoRay2(P(50, -50), GeoVector2.YAxis), new GeoRay2(P(50, 30), GeoVector2.YAxis), new GeoRay2(P(10, 10), -GeoVector2.XAxis) })
            {
                Assert.Equal(ray.CollidesWith(other, Tolerance.Global), ray.CollidesWith(other));
                Assert.Equal(ray.DistanceTo(other, Tolerance.Global), ray.DistanceTo(other));
                Assert.Equal(ray.TryIntersectWith(other, out GeoPoint2 within, Tolerance.Global), ray.TryIntersectWith(other, out GeoPoint2 plain));
                Assert.Equal(within, plain);
                Assert.Equal(ray.GetShortestLineTo(other, Tolerance.Global), ray.GetShortestLineTo(other));
            }
        }

        #endregion

        #region A segment (D9)

        [Fact]
        public void ASegmentAcrossTheRay_IsCrossedAtFiftyNought()
        {
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(50, -10), P(50, 10));

            Assert.True(ray.CollidesWith(segment, Strict));
            Assert.Equal(0.0, ray.DistanceTo(segment, Strict));
            Assert.True(ray.TryIntersectWith(segment, out GeoPoint2 crossing, Strict));
            AssertNear(P(50, 0), crossing);
            AssertLine(ray.GetShortestLineTo(segment, Strict), P(50, 0), P(50, 0));
        }

        [Fact]
        public void ASegmentAThousandAlong_IsCrossedThere_HoweverFarItIs()
        {
            // The cut reaches past the segment, so nothing far along is missed: (1000, 0), and (100 000, 0).
            GeoRay2 ray = AlongX();

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(1000, -10), P(1000, 10)), out GeoPoint2 near, Strict));
            AssertNear(P(1000, 0), near);
            Assert.True(ray.TryIntersectWith(new GeoLine2(P(100000, -10), P(100000, 10)), out GeoPoint2 far, Strict));
            AssertNear(P(100000, 0), far, 1E-6);
        }

        [Fact]
        public void ASegmentThroughTheOrigin_IsCrossedAtTheOrigin()
        {
            GeoRay2 ray = AlongX();

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(0, -10), P(0, 10)), out GeoPoint2 crossing, Strict));
            AssertNear(P(0, 0), crossing);
        }

        [Fact]
        public void ASegmentFiveTenThousandthsBehindTheOrigin_IsCrossedAtTheOrigin_AndTwoThousandthsBehindItIsNot()
        {
            // At x = -0.0005 the crossing is within the tolerance behind the origin and is brought onto the ray at the
            // origin; at x = -0.002 it is not, and the segment is 0.002 from the origin.
            GeoRay2 ray = AlongX();

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(-0.0005, -10), P(-0.0005, 10)), out GeoPoint2 crossing, Strict));
            AssertNear(P(0, 0), crossing);
            Assert.True(ray.CollidesWith(new GeoLine2(P(-0.0005, -10), P(-0.0005, 10)), Strict));

            var behind = new GeoLine2(P(-0.002, -10), P(-0.002, 10));

            Assert.False(ray.TryIntersectWith(behind, out _, Strict));
            Assert.False(ray.CollidesWith(behind, Strict));
            Assert.Equal(0.002, ray.DistanceTo(behind, Strict), 12);
            AssertLine(ray.GetShortestLineTo(behind, Strict), P(0, 0), P(-0.002, 0));
        }

        [Fact]
        public void ASegmentEndingOnTheRay_MeetsItAtItsEnd()
        {
            // End-on at (50, 0); with its end 0.0005 above the ray it still meets it, 0.002 above it does not.
            GeoRay2 ray = AlongX();

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(50, 0), P(50, 10)), out GeoPoint2 onIt, Strict));
            AssertNear(P(50, 0), onIt);
            Assert.True(ray.TryIntersectWith(new GeoLine2(P(50, 0.0005), P(50, 10)), out GeoPoint2 nearIt, Strict));
            AssertNear(P(50, 0), nearIt, 1E-3);

            var above = new GeoLine2(P(50, 0.002), P(50, 10));

            Assert.False(ray.TryIntersectWith(above, out _, Strict));
            Assert.Equal(0.002, ray.DistanceTo(above, Strict), 12);
            AssertLine(ray.GetShortestLineTo(above, Strict), P(50, 0), P(50, 0.002));
        }

        [Fact]
        public void ASegmentParallelFiveAbove_IsFiveAway_AndItsLineSitsMidwayAlongTheOverlap()
        {
            // The segment from (10, 5) to (60, 5) runs alongside the ray over x 10 to 60; the segment shortest line takes the
            // middle of the overlap, (35, 0) up to (35, 5). Reaching back behind the origin, from (-20, 5), the overlap
            // starts at the origin and the middle is (30, 0).
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(10, 5), P(60, 5));

            Assert.False(ray.TryIntersectWith(segment, out _, Strict));
            Assert.False(ray.CollidesWith(segment, Strict));
            Assert.Equal(5.0, ray.DistanceTo(segment, Strict), 12);
            AssertLine(ray.GetShortestLineTo(segment, Strict), P(35, 0), P(35, 5));
            AssertLine(ray.GetShortestLineTo(new GeoLine2(P(-20, 5), P(60, 5)), Strict), P(30, 0), P(30, 5));
        }

        [Fact]
        public void ASegmentAlongTheRay_Touches_ButNamesNoCrossing()
        {
            // Collinear from (20, 0) to (40, 0): nought apart, refused as parallel, and the line sits at (30, 0).
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(20, 0), P(40, 0));

            Assert.True(ray.CollidesWith(segment, Strict));
            Assert.Equal(0.0, ray.DistanceTo(segment, Strict));
            Assert.False(ray.TryIntersectWith(segment, out _, Strict));
            AssertLine(ray.GetShortestLineTo(segment, Strict), P(30, 0), P(30, 0));
        }

        [Fact]
        public void ASegmentOnTheCarrierBehindTheOrigin_IsTwentyAway()
        {
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(-40, 0), P(-20, 0));

            Assert.False(ray.CollidesWith(segment, Strict));
            Assert.Equal(20.0, ray.DistanceTo(segment, Strict), 12);
            AssertLine(ray.GetShortestLineTo(segment, Strict), P(0, 0), P(-20, 0));
        }

        [Fact]
        public void ASegmentAcrossTheCarrierBehindTheOrigin_IsMeasuredFromTheOrigin()
        {
            // At x = -30 it would be crossed 30 behind: 30 away, from the origin to (-30, 0).
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(-30, -10), P(-30, 10));

            Assert.False(ray.CollidesWith(segment, Strict));
            Assert.False(ray.TryIntersectWith(segment, out _, Strict));
            Assert.Equal(30.0, ray.DistanceTo(segment, Strict), 12);
            AssertLine(ray.GetShortestLineTo(segment, Strict), P(0, 0), P(-30, 0));
        }

        [Fact]
        public void ASegmentRunningTwoTenThousandthsAlongside_TouchesTheRay_ButIsParallel()
        {
            // From (0, 0.0002) to (100, 0.0004): it draws apart by 2E-4 over the cut of 101, below the tolerance, so it is
            // parallel; its nearer end is 0.0002 from the ray, so they touch.
            GeoRay2 ray = AlongX();
            var segment = new GeoLine2(P(0, 0.0002), P(100, 0.0004));

            Assert.False(ray.TryIntersectWith(segment, out _, Strict));
            Assert.Equal(0.0002, ray.DistanceTo(segment, Strict), 12);
            Assert.True(ray.CollidesWith(segment, Strict));
        }

        [Fact]
        public void AShallowSegmentNearTheOrigin_IsParallel_AndTheSameSegmentFiveHundredAlong_IsCrossed()
        {
            // D10: the drift is measured over the cut, drawn as far as the segment reaches. A segment 10 long at a sine of
            // 1E-5 crossing the ray at x = 50 is cut at 56: 5.6E-4 of drift, parallel. The same at x = 500 is cut at 506:
            // 5.1E-3 of drift, crossed at (500, 0). Either way they are nought apart.
            GeoRay2 ray = AlongX();
            var near = new GeoLine2(P(45, 5E-5), P(55, -5E-5));
            var far = new GeoLine2(P(495, 5E-5), P(505, -5E-5));

            Assert.False(ray.TryIntersectWith(near, out _, Strict));
            Assert.True(ray.TryIntersectWith(far, out GeoPoint2 crossing, Strict));
            AssertNear(P(500, 0), crossing, 1E-6);
            Assert.Equal(0.0, ray.DistanceTo(near, Strict));
            Assert.Equal(0.0, ray.DistanceTo(far, Strict));
        }

        [Fact]
        public void ASegmentCrossingAtAShallowAngleFarOut_IsCrossed()
        {
            // From (0, 5) to (2000, -5): sine 0.005 over a cut of 2001, ten units of drift; crossed at (1000, 0).
            GeoRay2 ray = AlongX();

            Assert.True(ray.TryIntersectWith(new GeoLine2(P(0, 5), P(2000, -5)), out GeoPoint2 crossing, Strict));
            AssertNear(P(1000, 0), crossing, 1E-9);
        }

        [Fact]
        public void TheSegmentFormsWithoutATolerance_AnswerAsTheGlobalToleranceDoes()
        {
            GeoRay2 ray = AlongX();

            foreach (GeoLine2 segment in new[] { new GeoLine2(P(50, -10), P(50, 10)), new GeoLine2(P(10, 5), P(60, 5)), new GeoLine2(P(-30, -10), P(-30, 10)) })
            {
                Assert.Equal(ray.CollidesWith(segment, Tolerance.Global), ray.CollidesWith(segment));
                Assert.Equal(ray.DistanceTo(segment, Tolerance.Global), ray.DistanceTo(segment));
                Assert.Equal(ray.TryIntersectWith(segment, out GeoPoint2 within, Tolerance.Global), ray.TryIntersectWith(segment, out GeoPoint2 plain));
                Assert.Equal(within, plain);
                Assert.Equal(ray.GetShortestLineTo(segment, Tolerance.Global), ray.GetShortestLineTo(segment));
            }
        }

        #endregion
    }
}
