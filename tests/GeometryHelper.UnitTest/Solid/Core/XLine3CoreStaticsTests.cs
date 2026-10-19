using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Shapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// How an infinite line in space is cut into a segment to be asked of a shape (<see cref="XLine3"/>), where its foot is,
    /// and where it comes nearest a ray, another line and a plane, with the numbers worked out by hand.
    /// </summary>
    /// <remarks>
    /// The cut runs from N - m to F + m along the line, N and F being the least and the greatest distance along it at which
    /// any point of the shape projects, and m a millimetre or a hundred point tolerances. The shapes are those of
    /// <see cref="XLine3Shapes"/>.
    /// </remarks>
    public class XLine3CoreStaticsTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static void AssertEnds(GeoLine3 cut, GeoPoint3 start, GeoPoint3 end)
        {
            Assert.True(cut.StartPoint.DistanceTo(start) <= 1E-9, "starts at " + cut.StartPoint + ", not " + start);
            Assert.True(cut.EndPoint.DistanceTo(end) <= 1E-9, "ends at " + cut.EndPoint + ", not " + end);
        }

        #region The cut

        [Fact]
        public void TheCutPastTheBox_ReachesAMillimetreOutsideItsCornersOnBothSides()
        {
            // The line from (-50, 50, 10) along X: the corners at x = 0 and 100 are 50 and 150 along; the cut is from 49 to 151, so from
            // (-1, 50, 10) to (101, 50, 10).
            var line = X(-50, 50, 10, 1, 0, 0);

            AssertEnds(XLine3.CutPast(line, Aabb(), Strict), P(-1, 50, 10), P(101, 50, 10));
            AssertEnds(XLine3.CutPast(line, Obb(), Strict), P(-1, 50, 10), P(101, 50, 10));
            AssertEnds(XLine3.CutPast(line, SolidBox(), Strict), P(-1, 50, 10), P(101, 50, 10));
            AssertEnds(XLine3.CutPast(line, Square(), Strict), P(-1, 50, 10), P(101, 50, 10));
            AssertEnds(XLine3.CutPast(line, Plate(), Strict), P(-1, 50, 10), P(101, 50, 10));
        }

        [Fact]
        public void TheCutRunsTheWayTheLineRuns_WhateverSideItsOriginIsOn()
        {
            // From (500, 50, 10) along -X the corners are 500 and 400 along; the cut is from 399 to 501: (101, 50, 10) to (-1, 50, 10).
            var line = X(500, 50, 10, -1, 0, 0);

            AssertEnds(XLine3.CutPast(line, Aabb(), Strict), P(101, 50, 10), P(-1, 50, 10));
        }

        [Fact]
        public void TheOriginIsNotPartOfTheCut_AShapeAMillionAlongIsAskedAsTheStretchRoundIt()
        {
            var line = X(-1E6, 50, 10, 1, 0, 0);
            GeoLine3 cut = XLine3.CutPast(line, Aabb(), Strict);

            AssertEnds(cut, P(-1, 50, 10), P(101, 50, 10));
            Assert.InRange(cut.Length, 101.999, 102.001);
        }

        [Fact]
        public void TheMarginIsAMillimetreOrAHundredPointTolerancesWhicheverIsMore()
        {
            var line = X(-50, 50, 10, 1, 0, 0);

            Assert.InRange(XLine3.CutPast(line, Aabb(), new Tolerance(1E-3, 1E-5)).Length, 101.999, 102.001);
            Assert.InRange(XLine3.CutPast(line, Aabb(), new Tolerance(5E-3, 1E-5)).Length, 101.999, 102.001);
            Assert.InRange(XLine3.CutPast(line, Aabb(), new Tolerance(5E-2, 1E-5)).Length, 109.999, 110.001);
        }

        [Fact]
        public void ARoundShapeIsCutPastItsCentreBroughtOntoTheLine_LessAndPlusItsRadius()
        {
            // The centre at the origin is 100 along the line from (-100, 30, 7); the circle of radius 50 is cut from 49 to 151.
            var line = X(-100, 30, 7, 1, 0, 0);

            AssertEnds(XLine3.CutPast(line, Circle(), Strict), P(-51, 30, 7), P(51, 30, 7));
        }

        [Fact]
        public void AnEllipseIsCutPastItsMajorRadius()
        {
            // Centre 150 along, major radius 100: from 49 to 251.
            var line = X(-150, 30, 7, 1, 0, 0);

            AssertEnds(XLine3.CutPast(line, Oval(), Strict), P(-101, 30, 7), P(101, 30, 7));
        }

        [Fact]
        public void ANearlyStraightArcIsCutPastItsEndsAndNotItsCircle_WhateverTheRadius()
        {
            // The circle of radius 1E6 about (0, -1E6, 0), the arc of 1E-4 radian over its top: 100 long, from (50, ~0, 0) to (-50, ~0, 0).
            double radius = 1E6;
            double half = 5E-5;
            var center = P(0, -radius, 0);
            GeoArc3 arc = ArcFrom(center, GeoVector3.ZAxis, radius, P(radius * Math.Sin(half), radius * Math.Cos(half) - radius, 0), 2 * half);
            var line = X(-80, 0, 0, 1, 0, 0);
            GeoLine3 cut = XLine3.CutPast(line, arc, Strict);

            // The ends at x = 50 and -50 are 130 and 30 along; the extreme of the circle along X is 1E6 away and not on the arc.
            Assert.InRange(cut.Length, 100.0, 104.0);
            Assert.InRange(cut.StartPoint.X, -52, -49);
            Assert.InRange(cut.EndPoint.X, 49, 52);
        }

        [Fact]
        public void AnArcThatHoldsTheFarthestPointOfItsCircle_IsCutPastIt()
        {
            // The arc from (50, 0, 0) over the top to (-50, 0, 0) does not hold the point of its circle furthest along +X, which is its
            // start, but the half from (0, -50, 0) round through (50, 0, 0) to (0, 50, 0) does, 50 past its ends.
            GeoArc3 right = ArcFrom(P(0, 0, 0), GeoVector3.ZAxis, 50, P(0, -50, 0), Math.PI);
            var line = X(-100, 10, 0, 1, 0, 0);
            GeoLine3 cut = XLine3.CutPast(line, right, Strict);

            // The ends project at 100 along; the point (50, 0, 0) at 150: the cut is from 99 to 151.
            AssertEnds(cut, P(-1, 10, 0), P(51, 10, 0));
        }

        [Fact]
        public void ALoopAndAChainAreCutPastTheCornersAndTheArcsExtremesOnly()
        {
            // The slot reaches from x = -25 to 225 along its round ends; the line from (-300, 25, 0) along X: from 274 to 526.
            AssertEnds(XLine3.CutPast(X(-300, 25, 0, 1, 0, 0), Slot(), Strict), P(-26, 25, 0), P(226, 25, 0));

            // The hook's half circle reaches x = 150: from the corner at 0 to 150, so from 49 to 201 from (-50, 50, 0).
            AssertEnds(XLine3.CutPast(X(-50, 50, 0, 1, 0, 0), Hook(), Strict), P(-1, 50, 0), P(151, 50, 0));
        }

        [Fact]
        public void ASegmentIsCutPastItsTwoEnds_WhichEverIsNearer()
        {
            // Both ends of the segment at x = 50 are 50 along the line from the origin along X: the cut is from 49 to 51.
            AssertEnds(XLine3.CutPast(X(0, 0, 0, 1, 0, 0), Segment(), Strict), P(49, 0, 0), P(51, 0, 0));
            AssertEnds(XLine3.CutPast(X(0, 0, 0, 1, 0, 0), new GeoLine3(P(20, 5, 5), P(80, 5, 5)), Strict), P(19, 0, 0), P(81, 0, 0));
        }

        [Fact]
        public void ALineOfNoDirection_IsCutToItsOrigin_AndANaNIsCarried()
        {
            GeoLine3 cut = XLine3.CutPast(default(GeoXLine3), Aabb(), Strict);

            Assert.Equal(P(0, 0, 0), cut.StartPoint);
            Assert.Equal(P(0, 0, 0), cut.EndPoint);

            GeoLine3 carried = XLine3.CutPast(new GeoXLine3(P(double.NaN, 0, 0), GeoVector3.XAxis), Aabb(), Strict);

            Assert.True(double.IsNaN(carried.StartPoint.X) && double.IsNaN(carried.EndPoint.X));
        }

        [Fact]
        public void ACutPastNothing_IsRefused()
        {
            var line = X(0, 0, 0, 1, 0, 0);

            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoSolid3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoPolygon3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoPolyline3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoFace3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoPolygonArc3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine3.CutPast(line, (GeoPolylineArc3)null, Strict));
        }

        [Fact]
        public void TheFoot_IsTheProjectionOnTheLine_AheadOfTheOriginAndBehindIt()
        {
            var line = X(10, 20, 30, 0, 1, 0);

            Assert.Equal(P(10, 45, 30), XLine3.Project(line, P(99, 45, -7)));
            Assert.Equal(P(10, -5, 30), XLine3.Project(line, P(-3, -5, 0)));
        }

        #endregion

        #region Where two lines come nearest

        [Fact]
        public void TwoLinesThatCross_ComeNearestAtTheCrossing_ANoughtApart()
        {
            var along = X(0, 0, 0, 1, 0, 0);
            var across = X(50, -50, 0, 0, 1, 0);

            Assert.True(XLine3.TryBridge(along, across, Strict, out double first, out double second, out double gap));
            Assert.Equal(50.0, first, 12);
            Assert.Equal(50.0, second, 12);
            Assert.InRange(gap, 0.0, 1E-12);
        }

        [Fact]
        public void TwoSkewLines_ComeNearestAtTheEndsOfTheirCommonPerpendicular_AsFarApartAsItIsLong()
        {
            var along = X(0, 0, 0, 1, 0, 0);
            var over = X(7, 3, 30, 0, 1, 0);

            Assert.True(XLine3.TryBridge(along, over, Strict, out double first, out double second, out double gap));
            Assert.Equal(7.0, first, 12);
            Assert.Equal(-3.0, second, 12);
            Assert.Equal(30.0, gap, 12);
        }

        [Fact]
        public void TwoLinesAskedEitherWayRound_GiveTheSameGapToTheBit_AndTheDistancesAlongSwapped()
        {
            var random = new Random(5);

            for (int i = 0; i < 2000; i++)
            {
                var a = new GeoXLine3(P(random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100), V(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));
                var b = new GeoXLine3(P(random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100), V(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));

                Assert.True(XLine3.TryBridge(a, b, Strict, out double aFirst, out double aSecond, out double aGap));
                Assert.True(XLine3.TryBridge(b, a, Strict, out double bFirst, out double bSecond, out double bGap));
                Assert.Equal(aGap, bGap);
                Assert.Equal(aFirst, bSecond);
                Assert.Equal(aSecond, bFirst);
                Assert.Equal(a.TryIntersectWith(b, out GeoPoint3[] one, Strict), b.TryIntersectWith(a, out GeoPoint3[] other, Strict));
                Assert.Equal(one, other);

                GeoLine3 forward = a.GetShortestLineTo(b, Strict);
                GeoLine3 backward = b.GetShortestLineTo(a, Strict);

                Assert.Equal(forward.StartPoint, backward.EndPoint);
                Assert.Equal(forward.EndPoint, backward.StartPoint);
            }
        }

        [Fact]
        public void TwoLinesParallelByRounding_HaveNoCommonPerpendicular_AndAreAsFarApartAsTheyAre()
        {
            var along = X(0, 0, 0, 1, 0, 0);

            Assert.False(XLine3.TryBridge(along, X(0, 30, 0, -1, 0, 0), Strict, out _, out _, out _));
            Assert.Equal(30.0, along.DistanceTo(X(0, 30, 0, -1, 0, 0), Strict), 12);
            Assert.Equal(5.0, along.DistanceTo(X(0, 0, 5, 1, 1E-13, 0), Strict), 9);
            Assert.Equal(5.0, X(0, 0, 5, 1, 1E-13, 0).DistanceTo(along, Strict), 9);
        }

        [Fact]
        public void TwoLinesAtARealAngle_AlwaysComeNearest_WhateverTheToleranceAndHoweverFarOut()
        {
            // The line through (5000, 0, 0) along Y and the one through (0, 5000, 0) along X cross at (5000, 5000, 0), at a tolerance
            // of 1E-12 as at 1E-2 and at nought; the one through (0, 5000, 8) passes 8 over it.
            foreach (double point in new[] { 0.0, 1E-12, 1E-9, 1E-3, 1E-2 })
            {
                var tolerance = new Tolerance(point, 1E-5);
                var up = X(5000, 0, 0, 0, 1, 0);

                Assert.InRange(up.DistanceTo(X(0, 5000, 0, 1, 0, 0), tolerance), 0.0, 1E-9);
                Assert.InRange(X(0, 5000, 0, 1, 0, 0).DistanceTo(up, tolerance), 0.0, 1E-9);
                Assert.InRange(Math.Abs(up.DistanceTo(X(0, 5000, 8, 1, 0, 0), tolerance) - 8.0), 0.0, 1E-9);
                Assert.True(up.CollidesWith(X(0, 5000, 0, 1, 0, 0), tolerance));
            }
        }

        [Fact]
        public void TwoLinesAMicroradianOffPassingFiveApart_AreFiveApartWhicheverWayTheyTilt_AndCrossWhereTheTiltIsInTheirPlane()
        {
            // Along X, and the line through (0, 0, 5) along (1, 1E-9, 0): skew, their common perpendicular along Z, exactly 5 long.
            var along = X(0, 0, 0, 1, 0, 0);

            Assert.Equal(5.0, along.DistanceTo(X(0, 0, 5, 1, 1E-9, 0), Strict), 9);
            Assert.Equal(5.0, X(0, 0, 5, 1, 1E-9, 0).DistanceTo(along, Strict), 9);

            // The line through (0, 5, 0) along (1, -1E-9, 0) lies in the plane of the first and crosses it 5E9 along, where a double can
            // place it within a thousandth: they meet, whichever way round.
            Assert.InRange(along.DistanceTo(X(0, 5, 0, 1, -1E-9, 0), Strict), 0.0, 1E-3);
            Assert.InRange(X(0, 5, 0, 1, -1E-9, 0).DistanceTo(along, Strict), 0.0, 1E-3);

            // At 1E-13 the crossing would be 5E13 along, which is rounding: they are parallel, 5 apart.
            Assert.Equal(5.0, along.DistanceTo(X(0, 5, 0, 1, -1E-13, 0), Strict), 9);
            Assert.Equal(5.0, X(0, 5, 0, 1, -1E-13, 0).DistanceTo(along, Strict), 9);
        }

        [Fact]
        public void TheRuleIsTheSameFromBothSides_OnLinesNearItsLimitAtEveryTolerance()
        {
            var random = new Random(31);
            int different = 0;

            for (int i = 0; i < 20000; i++)
            {
                double tolerance = Math.Pow(10, -9 + 7 * random.NextDouble());
                double sine = Math.Pow(10, -16 + 8 * random.NextDouble());
                double scale = Math.Pow(10, 3 + 10 * random.NextDouble());
                var a = new GeoXLine3(P(scale * random.NextDouble(), scale * random.NextDouble(), scale * random.NextDouble()), GeoVector3.XAxis);
                var b = new GeoXLine3(P(scale * random.NextDouble(), scale * random.NextDouble(), scale * random.NextDouble()), new GeoVector3(1, sine * (random.Next(2) == 0 ? 1 : -1), sine * (random.Next(2) == 0 ? 1 : -1)));
                var t = new Tolerance(tolerance, 1E-5);

                if (a.DistanceTo(b, t) != b.DistanceTo(a, t) || a.CollidesWith(b, t) != b.CollidesWith(a, t))
                {
                    different++;
                }
            }

            Assert.Equal(0, different);
        }

        [Fact]
        public void ALineAndARay_ComeNearestWhereTheRayReachesTheCommonPerpendicular_AndOtherwiseAtItsOrigin()
        {
            var along = X(0, 0, 0, 1, 0, 0);

            // The ray from (50, 30, 40) along -Y reaches y = 0 after 30; along +Y it never does.
            Assert.True(XLine3.TryBridge(along, new GeoRay3(P(50, 30, 40), V(0, -1, 0)), Strict, out double onLine, out double onRay, out double gap));
            Assert.Equal(50.0, onLine, 12);
            Assert.Equal(30.0, onRay, 12);
            Assert.Equal(40.0, gap, 12);

            Assert.True(XLine3.TryBridge(along, new GeoRay3(P(50, 30, 40), V(0, 1, 0)), Strict, out _, out double behind, out _));
            Assert.Equal(-30.0, behind, 12);
            Assert.Equal(50.0, along.DistanceTo(new GeoRay3(P(50, 30, 40), V(0, 1, 0)), Strict), 12);
            Assert.Equal(40.0, along.DistanceTo(new GeoRay3(P(50, 30, 40), V(0, -1, 0)), Strict), 12);
        }

        #endregion

        #region Where a line meets a plane

        [Fact]
        public void ASlantedLine_CrossesThePlaneAtTheDistanceItsHeightOverItsSlopeGives()
        {
            // From (0, 0, 10) along (1, 0, -1): the floor is 10 sqrt 2 along, at (10, 0, 0).
            var line = X(0, 0, 10, 1, 0, -1);

            Assert.True(XLine3.TryCrossPlane(line, Floor(), Strict, out double along));
            Assert.Equal(10 * Math.Sqrt(2), along, 12);
            AssertNearPoint(P(10, 0, 0), line.GetPointAtDistance(along));
        }

        private static void AssertNearPoint(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.True(expected.DistanceTo(actual) <= 1E-9, actual + " is not " + expected);
        }

        [Fact]
        public void ALineLyingInThePlane_NamesNoPlace_AndOneJustOffLevelWithinThePlanarToleranceIsInIt()
        {
            // Level to a sine of 1E-12 and within the planar tolerance of the plane: in it, nought from it, and no crossing.
            Assert.False(XLine3.TryCrossPlane(X(0, 0, 0, 1, 0, 0), Floor(), Strict, out _));
            Assert.False(XLine3.TryCrossPlane(X(0, 0, 0.0004, 1, 0, 0), Floor(), Strict, out _));
            Assert.False(XLine3.TryCrossPlane(X(0, 0, 0.0004, 1, 0, -1E-13), Floor(), Strict, out _));
            Assert.Equal(0.0, X(0, 0, 0.0004, 1, 0, -1E-13).DistanceTo(Floor(), Strict));

            // Seven above and running along it, it is just parallel.
            Assert.False(XLine3.TryCrossPlane(X(0, 0, 7, 1, 0, 0), Floor(), Strict, out _));
        }

        [Fact]
        public void ALineJustOffLevelAndWithinThePlanarTolerance_CrossesWhereItsHeightRunsOut()
        {
            // The line from (5, 5, 0.0004) along (1, 0, -0.01) crosses the floor 0.04 along, at (5.04, 5, 0).
            var line = X(5, 5, 0.0004, 1, 0, -0.01);

            Assert.True(XLine3.TryCrossPlane(line, Floor(), Strict, out double along));
            Assert.InRange(along, 0.0399, 0.0401);
        }

        [Fact]
        public void ALineParallelToThePlaneByRounding_NeverCrossesIt_ButAtARealAngleAlwaysDoes()
        {
            // 7 above, a sine of 1E-13: the crossing would be 7E13 along, which is rounding. At 1E-4 it is 70 000 along.
            Assert.False(XLine3.TryCrossPlane(X(0, 0, 7, 1, 0, -1E-13), Floor(), Strict, out _));
            Assert.Equal(7.0, X(0, 0, 7, 1, 0, -1E-13).DistanceTo(Floor(), Strict), 12);
            Assert.True(XLine3.TryCrossPlane(X(0, 0, 7, 1, 0, -1E-4), Floor(), Strict, out double far));
            Assert.InRange(far, 69999, 70001);
            Assert.Equal(0.0, X(0, 0, 7, 1, 0, -1E-4).DistanceTo(Floor(), Strict));
        }

        #endregion

        #region Order

        [Fact]
        public void OneLineComesBeforeAnother_ByOriginAndThenByDirection_AndNeverBothBeforeTheOther()
        {
            var a = X(0, 0, 0, 1, 0, 0);
            var b = X(0, 0, 1, 1, 0, 0);
            var c = X(0, 0, 0, 0, 1, 0);

            Assert.True(XLine3.Precedes(a, b));
            Assert.False(XLine3.Precedes(b, a));
            Assert.True(XLine3.Precedes(c, a) ^ XLine3.Precedes(a, c));
            Assert.False(XLine3.Precedes(a, a));
        }

        #endregion
    }
}
