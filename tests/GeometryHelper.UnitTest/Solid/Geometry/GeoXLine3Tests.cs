using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The infinite line in space as a value: how it is made, moved, measured along, cut to a box, compared and printed,
    /// which points are on it, how it turns into a ray, a segment and a line of the plane and back, and the line two planes
    /// share. Every expectation is worked out by hand.
    /// </summary>
    /// <remarks>
    /// The tolerances are a thousandth for points and 1E-5 for vectors, as the default, and 1 for points where a test shows
    /// the tolerance is read. Offsets of 0.0005 and 0.002 sit either side of the thousandth, a factor of two clear of it, so
    /// no answer here hangs on rounding at the boundary.
    /// </remarks>
    public class GeoXLine3Tests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z);

        private static void AssertNear(GeoPoint3 expected, GeoPoint3 actual, double within)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        #region Making one

        [Fact]
        public void TheDirectionIsMadeUnitLength_NineAlongZIsOneAlongZ()
        {
            var line = new GeoXLine3(P(1, 2, 3), V(0, 0, 9));

            Assert.Equal(P(1, 2, 3), line.Origin);
            Assert.Equal(1.0, line.Direction.Z, 14);
            Assert.Equal(0.0, line.Direction.X);
            Assert.True(line.IsValid);
        }

        [Fact]
        public void ADirectionOfThreeAndFour_IsSixAndEightTenths()
        {
            var line = new GeoXLine3(P(0, 0, 0), V(3, 0, 4));

            Assert.Equal(0.6, line.Direction.X, 14);
            Assert.Equal(0.8, line.Direction.Z, 14);
        }

        [Fact]
        public void ALineThroughTwoPoints_RunsFromTheFirstTowardsTheSecond()
        {
            var line = new GeoXLine3(P(1, 1, 1), P(1, 1, 11));

            Assert.Equal(P(1, 1, 1), line.Origin);
            Assert.Equal(1.0, line.Direction.Z, 14);
            Assert.Equal(P(1, 1, -4), line.GetPointAtDistance(-5));
        }

        [Fact]
        public void ADirectionOfNoLengthOrNoNumber_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoXLine3(P(0, 0, 0), V(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => new GeoXLine3(P(0, 0, 0), P(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => new GeoXLine3(P(0, 0, 0), V(double.NaN, 0, 1)));
        }

        [Fact]
        public void ACopyAndATwiceReversedLine_AreTheOriginalToTheLastBit()
        {
            var line = new GeoXLine3(P(0.1, 0.2, 0.3), V(0.7, -0.11, 0.3));

            Assert.Equal(line, line.Clone());
            Assert.Equal(line, line.Reverse().Reverse());
            Assert.True(line == line.Clone());
        }

        [Fact]
        public void TheReversedLine_KeepsItsOriginAndRunsExactlyTheOtherWay_AndIsTheSameLine()
        {
            var line = new GeoXLine3(P(10, 20, 30), V(1, 2, 2));
            GeoXLine3 back = line.Reverse();

            Assert.Equal(line.Origin, back.Origin);
            Assert.Equal(-line.Direction.X, back.Direction.X);
            Assert.Equal(-line.Direction.Y, back.Direction.Y);
            Assert.Equal(-line.Direction.Z, back.Direction.Z);
            Assert.True(line.IsSameLineAs(back, Strict));
            Assert.NotEqual(line, back);
            Assert.True(line != back);
        }

        [Fact]
        public void TranslatingMovesTheOriginAndKeepsTheDirectionToTheLastBit()
        {
            var line = new GeoXLine3(P(1, 2, 3), V(0.3, 0.4, 0.5));
            GeoXLine3 moved = line.Translate(V(10, -20, 5));

            Assert.Equal(P(11, -18, 8), moved.Origin);
            Assert.Equal(line.Direction, moved.Direction);
        }

        [Fact]
        public void ADefaultLine_IsMovedCopiedAndReversedWithoutThrowing()
        {
            GeoXLine3 none = default(GeoXLine3);

            Assert.Equal(none, none.Clone());
            Assert.Equal(none, none.Reverse());
            Assert.Equal(P(1, 2, 3), none.Translate(V(1, 2, 3)).Origin);
            Assert.False(none.IsValid);
        }

        #endregion

        #region Moving it

        [Fact]
        public void AQuarterTurnAboutTheZAxis_TurnsTheOriginAndTheDirection()
        {
            // (10, 0, 5) goes to (0, 10, 5); the direction (1, 2, 2)/3 goes to (-2, 1, 2)/3.
            var line = new GeoXLine3(P(10, 0, 5), V(1, 2, 2));
            GeoXLine3 turned = line.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));

            AssertNear(P(0, 10, 5), turned.Origin, 1E-12);
            Assert.Equal(-2.0 / 3, turned.Direction.X, 12);
            Assert.Equal(1.0 / 3, turned.Direction.Y, 12);
            Assert.Equal(2.0 / 3, turned.Direction.Z, 12);
            Assert.Equal(turned, GeoTransform3.RotationZ(Math.PI / 2).Transform(line));
        }

        [Fact]
        public void AnUnevenScaling_TurnsTheLine_AndTheDirectionIsMadeUnitLengthAgain()
        {
            // Doubling X takes (1, 1, 0) to (2, 1, 0), and the direction (1, 1, 0)/sqrt 2 to (2, 1, 0)/sqrt 5.
            var line = new GeoXLine3(P(1, 1, 1), V(1, 1, 0));
            GeoXLine3 stretched = line.TransformBy(GeoTransform3.Scaling(2, 1, 1));

            AssertNear(P(2, 1, 1), stretched.Origin, 1E-12);
            Assert.Equal(2 / Math.Sqrt(5), stretched.Direction.X, 12);
            Assert.Equal(1 / Math.Sqrt(5), stretched.Direction.Y, 12);
            Assert.Equal(0.0, stretched.Direction.Z, 12);
        }

        [Fact]
        public void ATransformationCollapsingTheDirection_IsRefused_AndNothingIsRefusedToo()
        {
            GeoTransform3 squash = GeoTransform3.Scaling(0, 1, 1);

            Assert.Throws<InvalidOperationException>(() => new GeoXLine3(P(5, 5, 5), GeoVector3.XAxis).TransformBy(squash));
            Assert.Throws<InvalidOperationException>(() => squash.Transform(new GeoXLine3(P(5, 5, 5), GeoVector3.XAxis)));
            Assert.Throws<ArgumentNullException>(() => new GeoXLine3(P(5, 5, 5), GeoVector3.XAxis).TransformBy(null));

            GeoXLine3 upright = new GeoXLine3(P(5, 5, 5), GeoVector3.YAxis).TransformBy(squash);

            AssertNear(P(0, 5, 5), upright.Origin, 1E-12);
            Assert.Equal(1.0, upright.Direction.Y, 14);
        }

        #endregion

        #region Along the line

        [Fact]
        public void FiveAlongTheLineFromOneNoughtNoughtAlongThreeFourNought_IsFourFourNought_AndFiveBehindIsMinusTwoMinusFourNought()
        {
            var line = new GeoXLine3(P(1, 0, 0), V(3, 4, 0));

            AssertNear(P(4, 4, 0), line.GetPointAtDistance(5), 1E-12);
            AssertNear(P(-2, -4, 0), line.GetPointAtDistance(-5), 1E-12);
        }

        [Fact]
        public void TheDistanceAtAPointOffTheLine_IsWhereItsFootFalls_BehindTheOriginToo()
        {
            var line = new GeoXLine3(P(10, 20, 30), GeoVector3.XAxis);

            Assert.Equal(40.0, line.GetDistanceAtPoint(P(50, 40, 30)), 12);
            Assert.Equal(-40.0, line.GetDistanceAtPoint(P(-30, 40, 99)), 12);
        }

        [Fact]
        public void TheClosestPointToAPointBesideTheLine_IsItsFoot_BehindTheOriginAsAheadOfIt()
        {
            var line = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            AssertNear(P(50, 0, 0), line.GetClosestPointOnBoundary(P(50, 40, 30)), 1E-12);
            AssertNear(P(-30, 0, 0), line.GetClosestPointOnBoundary(P(-30, 40, 30)), 1E-12);
        }

        [Fact]
        public void ToLine_IsTheSegmentBetweenTwoSignedDistances_RunningBackwardsWhenTheSecondIsTheSmaller()
        {
            var line = new GeoXLine3(P(10, 20, 30), GeoVector3.XAxis);
            GeoLine3 segment = line.ToLine(-30, 70);

            AssertNear(P(-20, 20, 30), segment.StartPoint, 1E-12);
            AssertNear(P(80, 20, 30), segment.EndPoint, 1E-12);

            GeoLine3 backwards = line.ToLine(70, -30);

            AssertNear(P(80, 20, 30), backwards.StartPoint, 1E-12);
            AssertNear(P(-20, 20, 30), backwards.EndPoint, 1E-12);
        }

        [Fact]
        public void ToLine_RefusesADistanceThatIsNaNOrInfinite_ButNotANegativeOne()
        {
            var line = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            Assert.Throws<ArgumentOutOfRangeException>(() => line.ToLine(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.ToLine(0, double.PositiveInfinity));
            Assert.Equal(-5.0, line.ToLine(-5, 5).StartPoint.X);
        }

        #endregion

        #region A ray, a segment and a line of the plane

        [Fact]
        public void ToRay_IsTheHalfAheadOfTheOrigin_AndTheOtherHalfIsTheReversedLinesRay()
        {
            var line = new GeoXLine3(P(1, 2, 3), V(0, 3, 4));
            GeoRay3 ahead = line.ToRay();
            GeoRay3 behind = line.Reverse().ToRay();

            Assert.Equal(line.Origin, ahead.Origin);
            Assert.Equal(line.Direction, ahead.Direction);
            Assert.Equal(line.Direction.Negate(), behind.Direction);
        }

        [Fact]
        public void ARayBecomesALineAndBack_ToTheLastBit()
        {
            var ray = new GeoRay3(P(1, 2, 3), V(0.3, -0.4, 0.5));
            GeoXLine3 line = ray.ToXLine();

            Assert.Equal(ray.Origin, line.Origin);
            Assert.Equal(ray.Direction, line.Direction);
            Assert.Equal(ray, line.ToRay());
        }

        [Fact]
        public void ALineWithNoDirection_HasNoRay_AndARayWithNoneHasNoLine()
        {
            Assert.Throws<ArgumentException>(() => default(GeoXLine3).ToRay());
            Assert.Throws<ArgumentException>(() => default(GeoRay3).ToXLine());
        }

        [Fact]
        public void ASegmentGivesTheLineThroughItsStartAlongTheWayItRuns()
        {
            var segment = new GeoLine3(P(0, 0, 0), P(100, 0, 0));
            GeoXLine3 line = segment.ToXLine();
            GeoXLine3 back = segment.Reverse().ToXLine();

            Assert.Equal(P(0, 0, 0), line.Origin);
            Assert.Equal(1.0, line.Direction.X, 14);
            Assert.Equal(P(100, 0, 0), back.Origin);
            Assert.Equal(-1.0, back.Direction.X, 14);
            Assert.True(line.IsSameLineAs(back, Strict));
        }

        [Fact]
        public void ASegmentTooShortToHaveADirection_HasNoLine()
        {
            Assert.Throws<ArgumentException>(() => new GeoLine3(P(5, 5, 5), P(5, 5, 5)).ToXLine());
        }

        [Fact]
        public void ALineLaidOutInTheFloorFrame_DropsItsHeightAndItsTiltAndComesBackOnTheFloor()
        {
            var floor = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

            // The line from (0, 0, 10) along (1, 0, -1), heading down at 45 degrees, is laid out as the line from the origin along X.
            GeoXLine2 flat = new GeoXLine3(P(0, 0, 10), V(1, 0, -1)).ProjectToXLine2(floor);

            Assert.Equal(0.0, flat.Origin.X);
            Assert.Equal(0.0, flat.Origin.Y);
            Assert.Equal(1.0, flat.Direction.X, 14);
            Assert.Equal(0.0, flat.Direction.Y, 14);

            // The line through (10, 20) along Y stands on the floor as the line through (10, 20, 0) along Y.
            GeoXLine3 lifted = new GeoXLine2(new GeoPoint2(10, 20), GeoVector2.YAxis).ToXLine3(floor);

            Assert.Equal(P(10, 20, 0), lifted.Origin);
            Assert.Equal(1.0, lifted.Direction.Y, 14);
            Assert.Equal(lifted, PlanarMap3(floor, new GeoXLine2(new GeoPoint2(10, 20), GeoVector2.YAxis)));
        }

        private static GeoXLine3 PlanarMap3(GeoCoordinateSystem3 frame, GeoXLine2 line) => GeometryHelper.Core.PlanarMap.ToXLine3(frame, line);

        [Fact]
        public void ALineAlongTheNormalOfTheFrame_HasNoDirectionInItsPlane()
        {
            var floor = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

            Assert.Throws<ArgumentException>(() => new GeoXLine3(P(1, 2, 3), GeoVector3.ZAxis).ProjectToXLine2(floor));
        }

        [Fact]
        public void ALineLaidOutAndPutBack_IsTheLineOnTheFloorUnderIt()
        {
            var tilted = new GeoCoordinateSystem3(P(5, 6, 7), V(1, 1, 0), V(0, 0, 1));
            var line = new GeoXLine3(P(5, 6, 7), V(1, 1, 0));

            // Along the first axis of the frame, through its origin: laid out along X of the plane through the origin and put back.
            GeoXLine3 back = line.ProjectToXLine2(tilted).ToXLine3(tilted);

            AssertNear(line.Origin, back.Origin, 1E-12);
            Assert.True(line.IsSameLineAs(back, Strict));
        }

        #endregion

        #region Cutting it to a box

        private static GeoAabb3 Box() => new GeoAabb3(P(0, 0, 0), P(100, 100, 20));

        private static void AssertEnds(GeoLine3? cut, GeoPoint3 start, GeoPoint3 end, double within = 1E-9)
        {
            Assert.True(cut.HasValue, "no segment");
            Assert.True(cut.Value.StartPoint.DistanceTo(start) <= within, "starts at " + cut.Value.StartPoint + ", not " + start);
            Assert.True(cut.Value.EndPoint.DistanceTo(end) <= within, "ends at " + cut.Value.EndPoint + ", not " + end);
        }

        [Fact]
        public void TheLineAlongXThroughTheBox_IsClippedToItsTwoSides()
        {
            AssertEnds(new GeoXLine3(P(-50, 50, 10), GeoVector3.XAxis).ClipTo(Box()), P(0, 50, 10), P(100, 50, 10));
        }

        [Fact]
        public void TheClippedSegment_RunsTheWayTheLineRuns_WhateverItsOrigin()
        {
            AssertEnds(new GeoXLine3(P(500, 50, 10), new GeoVector3(-1, 0, 0)).ClipTo(Box()), P(100, 50, 10), P(0, 50, 10));
            AssertEnds(new GeoXLine3(P(50, 50, 10), GeoVector3.XAxis).ClipTo(Box()), P(0, 50, 10), P(100, 50, 10));
        }

        [Fact]
        public void ADiagonalLine_IsClippedWhereItEntersAndLeavesTheBox()
        {
            // From (-10, -10, -10) along (1, 1, 1): in at the corner (0, 0, 0), out through the top at (20, 20, 20).
            AssertEnds(new GeoXLine3(P(-10, -10, -10), V(1, 1, 1)).ClipTo(Box()), P(0, 0, 0), P(20, 20, 20), 1E-9);
        }

        [Fact]
        public void ALineMissingTheBox_GivesNothing()
        {
            Assert.Null(new GeoXLine3(P(150, 0, 10), GeoVector3.YAxis).ClipTo(Box()));
            Assert.Null(new GeoXLine3(P(-50, 50, 30), GeoVector3.XAxis).ClipTo(Box()));
            Assert.Null(new GeoXLine3(P(-50, 50, 10), GeoVector3.XAxis).ClipTo(GeoAabb3.Empty));
        }

        [Fact]
        public void ALineAlongAnEdge_IsClippedToTheEdge()
        {
            AssertEnds(new GeoXLine3(P(-50, 0, 0), GeoVector3.XAxis).ClipTo(Box()), P(0, 0, 0), P(100, 0, 0));
        }

        [Fact]
        public void ALineHalfAThousandthOverTheTop_IsClippedToTheBoxGrownByTheTolerance_AndTwoThousandthsOverGivesNothing()
        {
            // The grown box reaches 0.001 past each side, so the line runs from x = -0.001 to 100.001 at the height it has.
            AssertEnds(new GeoXLine3(P(-50, 50, 20.0005), GeoVector3.XAxis).ClipTo(Box(), Strict), P(-0.001, 50, 20.0005), P(100.001, 50, 20.0005));
            Assert.Null(new GeoXLine3(P(-50, 50, 20.002), GeoVector3.XAxis).ClipTo(Box(), Strict));
        }

        [Fact]
        public void ALineTouchingACorner_GivesASegmentOfNoMoreThanTheToleranceAbout()
        {
            // Through (100, 100, 20) along (1, -1, 0) touches the box at the corner only.
            GeoLine3? cut = new GeoXLine3(P(100, 100, 20), V(1, -1, 0)).ClipTo(Box(), Strict);

            Assert.True(cut.HasValue);
            Assert.True(cut.Value.Length <= 3 * Strict.EqualPoint, cut.Value.Length.ToString());
            Assert.True(cut.Value.StartPoint.DistanceTo(P(100, 100, 20)) <= 3 * Strict.EqualPoint);
        }

        [Fact]
        public void TheFormWithoutATolerance_ClipsAsTheGlobalOneDoes()
        {
            var line = new GeoXLine3(P(-50, 50, 20.0005), GeoVector3.XAxis);

            Assert.Equal(line.ClipTo(Box(), Tolerance.Global), line.ClipTo(Box()));
            Assert.Equal(new GeoXLine3(P(-50, 50, 10), GeoVector3.XAxis).ClipTo(Box(), Tolerance.Global), new GeoXLine3(P(-50, 50, 10), GeoVector3.XAxis).ClipTo(Box()));
        }

        #endregion

        #region Predicates

        [Fact]
        public void APointOnTheLine_IsOnItAheadOfTheOriginAndBehindIt_AndTwoThousandthsBesideItIsNot()
        {
            var line = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            Assert.True(line.IsPointOn(P(100, 0, 0), Strict));
            Assert.True(line.IsPointOn(P(-1, 0, 0), Strict));
            Assert.True(line.IsPointOn(P(-5000, 0, 0.0005), Strict));
            Assert.False(line.IsPointOn(P(5, 0, 0.002), Strict));
            Assert.Equal(line.IsPointOn(P(100, 0, 0), Tolerance.Global), line.IsPointOn(P(100, 0, 0)));
        }

        [Fact]
        public void LocatingAPoint_SaysOnSideOrOutSideAndNeverInside()
        {
            var line = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            Assert.Equal(PointLocation.OnSide, line.Locate(P(-77, 0, 0), Strict));
            Assert.Equal(PointLocation.OutSide, line.Locate(P(5, 0, 1), Strict));
            Assert.Equal(line.Locate(P(5, 0, 1), Tolerance.Global), line.Locate(P(5, 0, 1)));
        }

        [Fact]
        public void LinesAtHalfADegree_AreParallel_AndAtTwoDegreesNot_EitherWayRound()
        {
            var along = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);
            double half = 0.5 * Math.PI / 180;
            double two = 2 * Math.PI / 180;
            var nearly = new GeoXLine3(P(0, 0, 7), V(Math.Cos(half), 0, Math.Sin(half)));
            var turned = new GeoXLine3(P(0, 0, 7), V(Math.Cos(two), 0, Math.Sin(two)));

            Assert.True(along.IsParallelTo(nearly));
            Assert.True(nearly.IsParallelTo(along));
            Assert.True(along.IsParallelTo(along.Reverse()));
            Assert.False(along.IsParallelTo(turned));
            Assert.False(turned.IsParallelTo(along));
        }

        [Fact]
        public void ALineIsParallelToASegmentAndToAPlaneByItsDirectionAlone()
        {
            var along = new GeoXLine3(P(0, 0, 7), GeoVector3.XAxis);

            Assert.True(along.IsParallelTo(new GeoLine3(P(0, 10, 0), P(-100, 10, 0))));
            Assert.False(along.IsParallelTo(new GeoLine3(P(0, 10, 0), P(0, 10, 100))));
            Assert.True(along.IsParallelTo(GeoPlane3.XY));
            Assert.True(new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis).IsParallelTo(GeoPlane3.XY));
            Assert.False(new GeoXLine3(P(0, 0, 0), GeoVector3.ZAxis).IsParallelTo(GeoPlane3.XY));
        }

        [Fact]
        public void TheSameLineFromAnotherOriginAndTheOtherWay_IsTheSameLine_ButNotOneAThousandthsOffOrAtAnAngle()
        {
            var line = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            Assert.True(line.IsSameLineAs(new GeoXLine3(P(500, 0, 0), new GeoVector3(-1, 0, 0)), Strict));
            Assert.True(line.IsSameLineAs(new GeoXLine3(P(-20, 0.0004, 0), GeoVector3.XAxis), Strict));
            Assert.False(line.IsSameLineAs(new GeoXLine3(P(0, 0, 0.002), GeoVector3.XAxis), Strict));
            Assert.False(line.IsSameLineAs(new GeoXLine3(P(0, 0, 0), V(1, 0.01, 0)), Strict));
            Assert.Equal(line.IsSameLineAs(line.Reverse(), Tolerance.Global), line.IsSameLineAs(line.Reverse()));
        }

        #endregion

        [Fact]
        public void TheExampleOfTheSolidGuide_GivesTheNumbersItPrints()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(100, 100, 20));
            var line = new GeoXLine3(P(-50, 50, 10), GeoVector3.XAxis);
            GeoPoint3[] crossings = line.GetIntersections(box);

            Assert.Equal(2, crossings.Length);
            AssertNear(P(0, 50, 10), crossings[0], 1E-9);
            AssertNear(P(100, 50, 10), crossings[1], 1E-9);
            Assert.True(line.CollidesWith(box));
            Assert.Equal(50.0, line.Translate(V(0, 100, 0)).DistanceTo(box), 9);
            AssertNear(P(-80, 50, 10), line.GetPointAtDistance(-30), 1E-12);
            AssertEnds(line.ClipTo(box), P(0, 50, 10), P(100, 50, 10));
            Assert.True(line.IsPointOn(P(-5000, 50, 10.0005)));

            // Two lines in space: skew, and nought where they cross.
            var along = new GeoXLine3(P(0, 0, 0), GeoVector3.XAxis);

            Assert.Equal(30.0, along.DistanceTo(new GeoXLine3(P(0, 0, 30), GeoVector3.YAxis)), 9);
            Assert.Equal(30.0, along.DistanceTo(new GeoXLine3(P(0, 0, 30), V(-1, 0, 0))), 9);
            Assert.Equal(0.0, along.DistanceTo(new GeoXLine3(P(0, 0, 0), GeoVector3.YAxis)), 9);

            // The floor and the wall x = 100 meet in the line along Y through (100, 0, 0).
            Assert.True(GeoPlane3.XY.TryGetIntersectionLine(new GeoPlane3(P(100, 0, 0), GeoVector3.XAxis), out GeoXLine3 shared));
            AssertNear(P(100, 0, 0), shared.Origin, 1E-9);
            Assert.Equal(1.0, shared.Direction.Y, 12);
        }

        #region Equality, validity and printing

        [Fact]
        public void EqualityReadsTheFields_AndTheHashFollowsIt()
        {
            var line = new GeoXLine3(P(1, 2, 3), V(1, 1, 1));
            var same = new GeoXLine3(P(1, 2, 3), V(1, 1, 1));
            var shifted = new GeoXLine3(P(2, 2, 3), V(1, 1, 1));

            Assert.True(line.Equals(same));
            Assert.True(line.Equals((object)same));
            Assert.Equal(line.GetHashCode(), same.GetHashCode());
            Assert.False(line.Equals(shifted));
            Assert.False(line.Equals("a line"));
            Assert.True(line == same);
            Assert.True(line != shifted);
        }

        [Fact]
        public void EqualWithinATolerance_ReadsTheOriginByThePointToleranceAndTheDirectionByTheVectorOne()
        {
            var line = new GeoXLine3(P(1, 2, 3), GeoVector3.XAxis);

            Assert.True(line.IsEqualTo(new GeoXLine3(P(1.0004, 2, 3), GeoVector3.XAxis), Strict));
            Assert.False(line.IsEqualTo(new GeoXLine3(P(1.002, 2, 3), GeoVector3.XAxis), Strict));
            Assert.False(line.IsEqualTo(new GeoXLine3(P(1, 2, 3), V(1, 0.01, 0)), Strict));
            Assert.Equal(line.IsEqualTo(line, Tolerance.Global), line.IsEqualTo(line));
        }

        [Fact]
        public void AConstructedLineIsValid_AndOneWithANaNOriginIsNot()
        {
            Assert.True(new GeoXLine3(P(1, 2, 3), GeoVector3.XAxis).IsValid);
            Assert.False(new GeoXLine3(P(double.NaN, 2, 3), GeoVector3.XAxis).IsValid);
        }

        [Fact]
        public void ItPrintsItsOriginAndItsDirection()
        {
            string text = new GeoXLine3(P(1, 2, 3), GeoVector3.XAxis).ToString();

            Assert.StartsWith("XLine3(Origin: ", text);
            Assert.Contains("Direction: ", text);
        }

        #endregion

        #region Where two planes meet

        [Fact]
        public void TheFloorAndAWall_MeetInTheLineAlongYThroughTheFootOfTheWall_AndTheOtherWayRoundItRunsTheOtherWay()
        {
            var wall = new GeoPlane3(P(100, 0, 0), GeoVector3.XAxis);

            Assert.True(GeoPlane3.XY.TryGetIntersectionLine(wall, out GeoXLine3 line));
            AssertNear(P(100, 0, 0), line.Origin, 1E-9);
            Assert.Equal(1.0, line.Direction.Y, 12);

            Assert.True(wall.TryGetIntersectionLine(GeoPlane3.XY, out GeoXLine3 other));
            AssertNear(P(100, 0, 0), other.Origin, 1E-9);
            Assert.Equal(-1.0, other.Direction.Y, 12);
            Assert.True(line.IsSameLineAs(other, Strict));
        }

        [Fact]
        public void TheLineTwoPlanesShare_IsTheRaysLineToTheLastBit()
        {
            var wall = new GeoPlane3(P(100, 0, 0), new GeoVector3(1, 0.3, 0));

            Assert.True(GeoPlane3.XY.TryIntersectWith(wall, out GeoRay3 ray, Strict));
            Assert.True(GeoPlane3.XY.TryGetIntersectionLine(wall, out GeoXLine3 line, Strict));
            Assert.Equal(ray.Origin, line.Origin);
            Assert.Equal(ray.Direction, line.Direction);
            Assert.True(GeoPlane3.XY.TryGetIntersectionLine(wall, out GeoXLine3 plain));
            Assert.Equal(line, plain);
        }

        [Fact]
        public void ParallelPlanesAreRefused_ApartAndCoincident()
        {
            Assert.False(GeoPlane3.XY.TryGetIntersectionLine(GeoPlane3.XY.Offset(5.0), out GeoXLine3 apart));
            Assert.False(GeoPlane3.XY.TryGetIntersectionLine(GeoPlane3.XY, out GeoXLine3 same, Strict));
            Assert.Equal(default(GeoXLine3), apart);
            Assert.Equal(default(GeoXLine3), same);
        }

        [Fact]
        public void TheLineTwoPlanesShare_LiesInBothAndIsNearestTheWorldOrigin()
        {
            var first = new GeoPlane3(P(3, 4, 5), V(1, 2, 3));
            var second = new GeoPlane3(P(-6, 7, 1), V(-2, 1, 4));

            Assert.True(first.TryGetIntersectionLine(second, out GeoXLine3 line, Strict));
            Assert.True(first.CollidesWith(line, Strict));
            Assert.True(second.CollidesWith(line, Strict));
            Assert.True(Math.Abs(first.SignedDistanceTo(line.GetPointAtDistance(1234.5))) <= 1E-6);
            Assert.True(Math.Abs(second.SignedDistanceTo(line.GetPointAtDistance(-1234.5))) <= 1E-6);
            Assert.True(Math.Abs(line.GetDistanceAtPoint(GeoPoint3.Origin)) <= 1E-9);
        }

        #endregion
    }
}
