using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// How an infinite line is cut into a segment to be asked of a shape (<see cref="XLine2"/>), where its foot is, and where it
    /// crosses a ray or another line, with the numbers worked out by hand.
    /// </summary>
    /// <remarks>
    /// The cut runs from N - m to F + m along the line, N and F being the least and the greatest distance along it at which
    /// any point of the shape projects, and m a millimetre or a hundred point tolerances. The shapes are those of
    /// <see cref="Ray2Shapes"/>.
    /// </remarks>
    public class XLine2CoreStaticsTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static void AssertEnds(GeoLine2 cut, GeoPoint2 start, GeoPoint2 end)
        {
            Assert.True(cut.StartPoint.DistanceTo(start) <= 1E-9, "starts at " + cut.StartPoint + ", not " + start);
            Assert.True(cut.EndPoint.DistanceTo(end) <= 1E-9, "ends at " + cut.EndPoint + ", not " + end);
        }

        [Fact]
        public void TheCutPastTheSquare_ReachesAMillimetreOutsideItsCornersOnBothSides()
        {
            // The line from (-50, 50) along X: the corners at x = 0 and 100 are 50 and 150 along; the cut is from 49 to 151, so from
            // (-1, 50) to (101, 50).
            var line = new GeoXLine2(P(-50, 50), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, Square(), Strict), P(-1, 50), P(101, 50));
            AssertEnds(XLine2.CutPast(line, Rectangle(), Strict), P(-1, 50), P(101, 50));
            AssertEnds(XLine2.CutPast(line, Plate(), Strict), P(-1, 50), P(101, 50));
        }

        [Fact]
        public void TheCutRunsTheWayTheLineRuns_WhateverSideItsOriginIsOn()
        {
            // From (500, 50) along -X the corners are 500 and 400 along; the cut is from 399 to 501: (101, 50) to (-1, 50).
            var line = new GeoXLine2(P(500, 50), new GeoVector2(-1, 0));

            AssertEnds(XLine2.CutPast(line, Square(), Strict), P(101, 50), P(-1, 50));
        }

        [Fact]
        public void TheOriginIsNotPartOfTheCut_AShapeAMillionAlongIsAskedAsTheStretchRoundIt()
        {
            var line = new GeoXLine2(P(-1E6, 50), GeoVector2.XAxis);
            GeoLine2 cut = XLine2.CutPast(line, Square(), Strict);

            AssertEnds(cut, P(-1, 50), P(101, 50));
            Assert.InRange(cut.Length, 101.999, 102.001);
        }

        [Fact]
        public void TheMarginIsAMillimetreOrAHundredPointTolerancesWhicheverIsMore()
        {
            var line = new GeoXLine2(P(-50, 50), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, Square(), new Tolerance(0.1, 1E-5)), P(-10, 50), P(110, 50));
            AssertEnds(XLine2.CutPast(line, Square(), new Tolerance(0.001, 1E-5)), P(-1, 50), P(101, 50));
        }

        [Fact]
        public void ARoundShapeIsCutPastItsCentreBroughtOntoTheLine_LessAndPlusItsRadius()
        {
            // The centre is 100 along; with the radius 50 the shape projects between 50 and 150, so the cut is 49 to 151.
            var line = new GeoXLine2(P(-100, 30), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, Circle(), Strict), P(-51, 30), P(51, 30));

            // An arc is cut past its own extent, not its circle's: this one runs from (43.30, 25) over the top to (-50, 0), so it
            // reaches 143.30 along the line at most, and its circle's far side at x = 50 is not part of it.
            AssertEnds(XLine2.CutPast(line, Arc(), Strict), P(-51, 30), P(44.30127018922193, 30));
        }

        [Fact]
        public void ANearlyStraightArcIsCutPastItsEndsAndNotItsCircle_WhateverTheRadius()
        {
            // From (0, 0) to (100, 0) with a bulge of 1E-10 the circle has a radius of 1.25E11; the cut is a hundred and two long,
            // not 2.5E11, as the line along X cuts it from its first end to its second.
            var line = new GeoXLine2(P(-10, 0), GeoVector2.XAxis);
            var flat = new GeoEdge2(P(0, 0), P(100, 0), 1E-10).ToArc();
            GeoLine2 cut = XLine2.CutPast(line, flat, Strict);

            // The ends of an arc on a circle of 1E11 are known to about 1E-5, so only to that.
            Assert.InRange(cut.Length, 101.9, 102.1);
            Assert.True(cut.StartPoint.DistanceTo(P(-1, 0)) <= 1E-3 && cut.EndPoint.DistanceTo(P(101, 0)) <= 1E-3, cut.ToString());

            // Square to its chord, the line meets its middle: the arc reaches 1E-8 off the chord and the cut stays 102 long.
            GeoLine2 across = XLine2.CutPast(new GeoXLine2(P(50, -30), GeoVector2.YAxis), flat, Strict);

            Assert.InRange(across.Length, 1.9, 2.1);
        }

        [Fact]
        public void ALoopWithANearlyStraightEdge_IsCutPastItsCornersAndTheArcsExtremesOnly()
        {
            var loop = new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 100), P(0, 100) }, new[] { 0.0, 1E-9, 0.0, 0.0 });
            GeoLine2 cut = XLine2.CutPast(new GeoXLine2(P(-50, 50), GeoVector2.XAxis), loop, Strict);

            // The arc bows 5E-8 past its chord, which the cut takes in.
            Assert.True(cut.StartPoint.DistanceTo(P(-1, 50)) <= 1E-6 && cut.EndPoint.DistanceTo(P(101, 50)) <= 1E-6, cut.ToString());
        }

        [Fact]
        public void AnEllipseIsCutPastItsMajorRadius()
        {
            var line = new GeoXLine2(P(-150, 0), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, Oval(), Strict), P(-101, 0), P(101, 0));
        }

        [Fact]
        public void ACurvedLoopAndAChainAreCutPastTheCircleOfEveryArcInThem()
        {
            // The slot reaches 25 past its corners at x = 0 and 200; the cut takes in the circles of its two round ends.
            var line = new GeoXLine2(P(-300, 25), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, Slot(), Strict), P(-26, 25), P(226, 25));
            AssertEnds(XLine2.CutPast(line, Hook(), new Tolerance(0.001, 1E-5)), P(-1, 25), P(151, 25));
        }

        [Fact]
        public void ASegmentIsCutPastItsTwoEnds_WhichEverIsNearer()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            AssertEnds(XLine2.CutPast(line, new GeoLine2(P(80, 5), P(20, 9)), Strict), P(19, 0), P(81, 0));
            AssertEnds(XLine2.CutPast(line, new GeoTriangle2(P(0, 0), P(100, 0), P(0, 100)), Strict), P(-1, 0), P(101, 0));
            AssertEnds(XLine2.CutPast(line, new GeoPolyline2(P(10, 0), P(30, 5), P(-20, 5)), Strict), P(-21, 0), P(31, 0));
        }

        [Fact]
        public void ALineOfNoDirection_IsCutToItsOrigin_AndANaNIsCarried()
        {
            GeoLine2 point = XLine2.CutPast(default(GeoXLine2), Square(), Strict);

            Assert.Equal(P(0, 0), point.StartPoint);
            Assert.Equal(P(0, 0), point.EndPoint);

            GeoLine2 lost = XLine2.CutPast(new GeoXLine2(P(double.NaN, 0), GeoVector2.XAxis), Square(), Strict);

            Assert.True(double.IsNaN(lost.StartPoint.X) && double.IsNaN(lost.EndPoint.X));
        }

        [Fact]
        public void ACutPastNothing_IsRefused()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.Throws<ArgumentNullException>(() => XLine2.CutPast(line, (GeoPolygon2)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine2.CutPast(line, (GeoPolyline2)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine2.CutPast(line, (GeoFace2)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine2.CutPast(line, (GeoPolygonArc2)null, Strict));
            Assert.Throws<ArgumentNullException>(() => XLine2.CutPast(line, (GeoPolylineArc2)null, Strict));
        }

        [Fact]
        public void TheFoot_IsTheProjectionOnTheLine_AheadOfTheOriginAndBehindIt()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.True(XLine2.Project(line, P(50, 40)).DistanceTo(P(50, 0)) <= 1E-12);
            Assert.True(XLine2.Project(line, P(-30, 40)).DistanceTo(P(-30, 0)) <= 1E-12);
        }

        [Fact]
        public void TwoLinesCross_WhereverTheirCarriersMeet_AndNowhereWhenParallelByRounding()
        {
            var along = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.True(XLine2.TryCross(along, new GeoXLine2(P(50, -50), GeoVector2.YAxis), Strict, out double at));
            Assert.Equal(50.0, at, 12);
            Assert.True(XLine2.TryCross(new GeoXLine2(P(50, -50), GeoVector2.YAxis), along, Strict, out double up));
            Assert.Equal(50.0, up, 12);
            Assert.False(XLine2.TryCross(along, new GeoXLine2(P(0, 5), new GeoVector2(-1, 0)), Strict, out _));
            Assert.False(XLine2.TryCross(along, new GeoXLine2(P(0, 5), new GeoVector2(1, 1E-17)), Strict, out _));
            Assert.False(XLine2.TryCross(along, new GeoXLine2(P(double.NaN, 5), GeoVector2.YAxis), Strict, out _));
        }

        [Fact]
        public void ALineAndARayCross_AtOrAheadOfTheOriginOfTheRay_OrWithinTheToleranceBehindItWhereThatIsAsked()
        {
            var along = new GeoXLine2(P(0, 0), GeoVector2.XAxis);
            var up = new GeoRay2(P(50, -30), GeoVector2.YAxis);
            var away = new GeoRay2(P(50, 30), GeoVector2.YAxis);
            var justPast = new GeoRay2(P(50, 0.0005), GeoVector2.YAxis);

            Assert.True(XLine2.TryCross(along, up, Strict, false, out double alongLine, out double alongRay));
            Assert.Equal(50.0, alongLine, 12);
            Assert.Equal(30.0, alongRay, 12);
            Assert.False(XLine2.TryCross(along, away, Strict, false, out _, out _));
            Assert.False(XLine2.TryCross(along, away, Strict, true, out _, out _));
            Assert.False(XLine2.TryCross(along, justPast, Strict, false, out _, out _));
            Assert.True(XLine2.TryCross(along, justPast, Strict, true, out _, out double behind));
            Assert.Equal(-0.0005, behind, 12);
        }

        [Fact]
        public void ACrossingOfTheLineAndASegmentOrARay_IsPlacedOnTheSegmentOrTheRay_FromBothSides()
        {
            var along = new GeoXLine2(P(0, 0), new GeoVector2(1, 1));
            var segment = new GeoLine2(P(0, 10), P(20, 10));
            var ray = new GeoRay2(P(20, 0), new GeoVector2(-1, 1));

            Assert.True(Intersection2.TryIntersectWith(segment, along, out GeoPoint2 onSegment, Strict));
            Assert.True(onSegment.DistanceTo(P(10, 10)) <= 1E-9);
            Assert.True(along.TryIntersectWith(segment, out GeoPoint2 fromLine, Strict));
            Assert.Equal(onSegment, fromLine);
            Assert.True(Intersection2.TryIntersectWith(ray, along, out GeoPoint2 onRay, Strict));
            Assert.True(onRay.DistanceTo(P(10, 10)) <= 1E-9);
            Assert.True(ray.TryIntersectWith(along, out GeoPoint2 fromRay, Strict));
            Assert.True(along.TryIntersectWith(ray, out GeoPoint2 fromLineSide, Strict));
            Assert.Equal(fromRay, fromLineSide);
        }

        [Fact]
        public void ACrossingBehindTheOriginOfTheRay_IsPlacedOnTheRayAtItsOrigin()
        {
            // The ray from (50, 0.0005) along Y meets the line along X at (50, 0), 0.0005 behind its origin: within the thousandth,
            // so it counts, and on the ray it is stopped at the origin.
            var along = new GeoXLine2(P(0, 0), GeoVector2.XAxis);
            var ray = new GeoRay2(P(50, 0.0005), GeoVector2.YAxis);

            Assert.True(Intersection2.TryIntersectWith(ray, along, out GeoPoint2 onRay, Strict));
            Assert.True(onRay.DistanceTo(P(50, 0.0005)) <= 1E-9);
            Assert.True(along.TryIntersectWith(ray, out GeoPoint2 fromLine, Strict));
            Assert.Equal(onRay, fromLine);
        }

        [Fact]
        public void TwoLinesAskedEitherWayRound_GiveTheSamePointAndTheSameShortestLineTurnedRound_ToTheBit()
        {
            // Crossing, side by side, and side by side with the nearer origins tied: each answer is worked out from the line that
            // comes first and turned round for the other.
            var pairs = new[]
            {
                (new GeoXLine2(P(0, 0), GeoVector2.XAxis), new GeoXLine2(P(50, -50), new GeoVector2(1, 2))),
                (new GeoXLine2(P(3, 4), new GeoVector2(1, 1)), new GeoXLine2(P(-7, 9), new GeoVector2(1, 1))),
                (new GeoXLine2(P(3, 4), new GeoVector2(1, 1)), new GeoXLine2(P(-7, 9), new GeoVector2(-1, -1))),
                (new GeoXLine2(P(0, 0), GeoVector2.XAxis), new GeoXLine2(P(0, 10), GeoVector2.XAxis)),
                (new GeoXLine2(P(0, 0), GeoVector2.XAxis), new GeoXLine2(P(10, 0), GeoVector2.XAxis)),
            };

            foreach (var (a, b) in pairs)
            {
                GeoLine2 forward = a.GetShortestLineTo(b, Strict);
                GeoLine2 backward = b.GetShortestLineTo(a, Strict);

                Assert.Equal(forward.StartPoint, backward.EndPoint);
                Assert.Equal(forward.EndPoint, backward.StartPoint);
                Assert.Equal(a.TryIntersectWith(b, out GeoPoint2 one, Strict), b.TryIntersectWith(a, out GeoPoint2 other, Strict));
                Assert.Equal(one, other);
            }
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E4)]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void ALineRebuiltFromTwoOfThePointsOfAnotherOffsetByTen_IsTenFromIt_AtCoordinatesUpToAMillion(double scale)
        {
            // The direction rebuilt from two points a hundred apart is a sine of about 1E-14 off at coordinates of 1E4 and 1E-11 at
            // 1E7: the two cross 1E15 along, or 1E12, where a double cannot place the crossing within a thousandth only for the
            // first. So up to a million the rebuilt line is parallel and 10 apart, in 100 000 pairs, either way round; at 1E7 the
            // crossing is placeable and 62 % of the pairs read nought, which is the limit of the rule.
            var random = new Random(8);
            int wrong = 0;

            for (int i = 0; i < 100000; i++)
            {
                double angle = random.NextDouble() * Math.PI;
                var original = new GeoXLine2(P(scale * random.NextDouble(), scale * random.NextDouble()), new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                GeoXLine2 offset = original.Offset(10);
                var rebuilt = new GeoXLine2(offset.GetPointAtDistance(-50), offset.GetPointAtDistance(50));

                if (Math.Abs(original.DistanceTo(rebuilt, Strict) - 10.0) > 1E-6 || Math.Abs(rebuilt.DistanceTo(original, Strict) - 10.0) > 1E-6 || original.CollidesWith(rebuilt, Strict))
                {
                    wrong++;
                }
            }

            Assert.Equal(0, wrong);
        }

        [Fact]
        public void TwoLinesAtARealAngle_AlwaysCross_WhateverTheToleranceAndHoweverFarOut()
        {
            // The line through (5000, 0) along Y and the one through (0, 5000) along X cross at (5000, 5000), at a tolerance of 1E-12
            // as at 1E-2 and at nought.
            foreach (double point in new[] { 0.0, 1E-12, 1E-9, 1E-3, 1E-2 })
            {
                var tolerance = new Tolerance(point, 1E-5);
                var up = new GeoXLine2(P(5000, 0), GeoVector2.YAxis);
                var along = new GeoXLine2(P(0, 5000), GeoVector2.XAxis);

                Assert.Equal(0.0, up.DistanceTo(along, tolerance));
                Assert.Equal(0.0, along.DistanceTo(up, tolerance));
                Assert.True(up.TryIntersectWith(along, out GeoPoint2 crossing, tolerance), "tolerance " + point);
                Assert.True(crossing.DistanceTo(P(5000, 5000)) <= 1E-6, crossing.ToString());
                Assert.True(up.CollidesWith(along, tolerance));
            }
        }

        [Fact]
        public void TwoLinesNearTheLimitOfTheRule_AreAsFarApartFromEitherSide()
        {
            // la through (-1E12, 0) along X and lb along (cos 1E-9, sin 1E-9) from 3E12 along it: asked either way round the same, where
            // measuring along the first only gave 0 one way and 1 000 the other.
            var la = new GeoXLine2(P(-1E12, 0), GeoVector2.XAxis);
            var direction = new GeoVector2(Math.Cos(1E-9), Math.Sin(1E-9));
            var lb = new GeoXLine2(P(3E12 * direction.X, 3E12 * direction.Y), direction);

            Assert.Equal(la.DistanceTo(lb, Strict), lb.DistanceTo(la, Strict));
            Assert.Equal(la.CollidesWith(lb, Strict), lb.CollidesWith(la, Strict));

            // Two lines at 0.3 radians, their origins 1E6 and 3E6 from the crossing, at a tolerance of 1E-9: they cross.
            var tight = new Tolerance(1E-9, 1E-5);
            var first = new GeoXLine2(P(-1E6, 0), GeoVector2.XAxis);
            var turned = new GeoVector2(Math.Cos(0.3), Math.Sin(0.3));
            var second = new GeoXLine2(P(3E6 * turned.X, 3E6 * turned.Y), turned);

            Assert.Equal(0.0, first.DistanceTo(second, tight));
            Assert.Equal(0.0, second.DistanceTo(first, tight));
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
                var a = new GeoXLine2(P(scale * random.NextDouble(), scale * random.NextDouble()), GeoVector2.XAxis);
                var b = new GeoXLine2(P(scale * random.NextDouble(), scale * random.NextDouble()), new GeoVector2(1, sine * (random.Next(2) == 0 ? 1 : -1)));
                var t = new Tolerance(tolerance, 1E-5);

                if (a.DistanceTo(b, t) != b.DistanceTo(a, t) || a.CollidesWith(b, t) != b.CollidesWith(a, t))
                {
                    different++;
                }
            }

            Assert.Equal(0, different);
        }

        [Fact]
        public void ALoopOfSevenCornersWithAnEdgeOfBulgeOneInTenBillion_IsTouchedByALineThatCrossesIt()
        {
            // The loop sits near (1E4, -3E4); its first edge is an arc on a circle of a radius of 1E11. The line crosses it twice,
            // as GetIntersections says, so it is nought from it and collides, where a cut as long as that circle gave 1.0000048
            // and no collision.
            var tolerance = new Tolerance(0.01, 1E-5);
            var loop = new GeoPolygonArc2(
                new[]
                {
                    P(9990.0414020358749, -29913.411238533416), P(9903.0670783605947, -29964.890028193284), P(9982.90047356723, -30017.299915798219),
                    P(9986.26860712989, -30095.644033875855), P(10042.199115901429, -30042.986733362188), P(10061.679080622835, -30044.700950502407),
                    P(10067.689902025786, -30017.467208106471)
                },
                new[] { 9.48552042711855E-11, -8.6952454776946669E-05, -7.9676751550136564E-06, 0.0027120569733493083, -0.25698857412533305, -0.10179449082435787, 2.9720468786414926E-05 });
            var line = new GeoXLine2(P(10767.250420656392, -29354.291150776437), new GeoVector2(0.72436188481049291, 0.6894199444705601));

            Assert.Equal(2, line.GetIntersections(loop, tolerance).Length);
            Assert.Equal(0.0, line.DistanceTo(loop, tolerance));
            Assert.Equal(0.0, loop.DistanceTo(line, tolerance));
            Assert.True(line.CollidesWith(loop, tolerance));
            Assert.True(loop.CollidesWith(line, tolerance));
        }
    }
}
