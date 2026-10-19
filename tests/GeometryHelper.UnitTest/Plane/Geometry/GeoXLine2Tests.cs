using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The infinite line in the plane as a value: how it is made, moved, measured along, cut to a box, compared and printed,
    /// which points are on it and which side of it they lie, and how it turns into a ray, a segment and back. Every
    /// expectation is worked out by hand.
    /// </summary>
    /// <remarks>
    /// The tolerances are a thousandth for points and 1E-5 for vectors, as the default, and 1 for points where a test shows
    /// the tolerance is read. Offsets of 0.0005 and 0.002 sit either side of the thousandth, a factor of two clear of it, so
    /// no answer here hangs on rounding at the boundary.
    /// </remarks>
    public class GeoXLine2Tests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static void AssertNear(GeoPoint2 expected, GeoPoint2 actual, double within)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        #region Making one

        [Fact]
        public void TheDirectionIsMadeUnitLength_NineAlongYIsOneAlongY()
        {
            var line = new GeoXLine2(P(1, 2), new GeoVector2(0, 9));

            Assert.Equal(P(1, 2), line.Origin);
            Assert.Equal(new GeoVector2(0, 1), line.Direction);
        }

        [Fact]
        public void ADirectionOfThreeAndFour_IsSixAndEightTenths()
        {
            var line = new GeoXLine2(P(0, 0), new GeoVector2(3, 4));

            Assert.InRange(line.Direction.X, 0.6 - 1E-15, 0.6 + 1E-15);
            Assert.InRange(line.Direction.Y, 0.8 - 1E-15, 0.8 + 1E-15);
        }

        [Fact]
        public void ALineThroughTwoPoints_RunsFromTheFirstTowardsTheSecond()
        {
            // From (1, 1) through (4, 5): along (3, 4), so (0.6, 0.8); (4, 5) is 5 along it and (-2, -3) is 5 behind.
            var line = new GeoXLine2(P(1, 1), P(4, 5));

            Assert.Equal(5.0, line.GetDistanceAtPoint(P(4, 5)), 12);
            Assert.Equal(-5.0, line.GetDistanceAtPoint(P(-2, -3)), 12);
            Assert.True(line.IsPointOn(P(-2, -3), Strict));
        }

        [Fact]
        public void ADirectionOfNoLengthOrNoNumber_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoXLine2(P(1, 2), GeoVector2.Zero));
            Assert.Throws<ArgumentException>(() => new GeoXLine2(P(1, 2), P(1, 2)));
            Assert.Throws<ArgumentException>(() => new GeoXLine2(P(1, 2), new GeoVector2(double.NaN, 1)));
            Assert.Throws<ArgumentException>(() => new GeoXLine2(P(1, 2), new GeoVector2(double.PositiveInfinity, 1)));
        }

        #endregion

        #region Copies, reversal and motion

        [Fact]
        public void ACopyAndATwiceReversedLine_AreTheOriginalToTheLastBit()
        {
            // (1, 2) normalized is a direction that normalizing again moves in its last digits, so a copy or a reversal that went
            // through the normalizing constructor would come back unequal.
            var line = new GeoXLine2(P(3, -7), new GeoVector2(1, 2));

            GeoXLine2 copy = line.Clone();
            GeoXLine2 twice = line.Reverse().Reverse();

            Assert.Equal(line, copy);
            Assert.True(line == copy);
            Assert.Equal(line, twice);
            Assert.Equal(line.Direction.X, twice.Direction.X);
            Assert.Equal(line.Direction.Y, twice.Direction.Y);
        }

        [Fact]
        public void TheReversedLine_KeepsItsOriginAndRunsExactlyTheOtherWay_AndIsTheSameLine()
        {
            var line = new GeoXLine2(P(3, -7), new GeoVector2(1, 2));
            GeoXLine2 reversed = line.Reverse();

            Assert.Equal(line.Origin, reversed.Origin);
            Assert.Equal(-line.Direction.X, reversed.Direction.X);
            Assert.Equal(-line.Direction.Y, reversed.Direction.Y);
            Assert.NotEqual(line, reversed);
            Assert.True(line != reversed);
            Assert.True(line.IsSameLineAs(reversed, Strict));
        }

        [Fact]
        public void TranslatingMovesTheOriginAndKeepsTheDirectionToTheLastBit()
        {
            var line = new GeoXLine2(P(3, -7), new GeoVector2(1, 2));
            GeoXLine2 moved = line.Translate(new GeoVector2(10, 20));

            Assert.Equal(P(13, 13), moved.Origin);
            Assert.Equal(line.Direction.X, moved.Direction.X);
            Assert.Equal(line.Direction.Y, moved.Direction.Y);
        }

        [Fact]
        public void ADefaultLine_IsMovedCopiedAndReversedWithoutThrowing()
        {
            GeoXLine2 moved = default(GeoXLine2).Translate(new GeoVector2(10, 20));

            Assert.Equal(P(10, 20), moved.Origin);
            Assert.Equal(GeoVector2.Zero, moved.Direction);
            Assert.Equal(default(GeoXLine2), default(GeoXLine2).Clone());
            Assert.False(default(GeoXLine2).IsValid);
        }

        [Fact]
        public void AQuarterTurnAboutTheOrigin_TurnsTheOriginAndTheDirection()
        {
            // (10, 0) goes to (0, 10); the direction (1, 2)/sqrt 5 goes to (-2, 1)/sqrt 5.
            var line = new GeoXLine2(P(10, 0), new GeoVector2(1, 2));
            GeoXLine2 turned = line.TransformBy(GeoTransform2.Rotation(Math.PI / 2));

            AssertNear(P(0, 10), turned.Origin, 1E-12);
            Assert.InRange(turned.Direction.X, -2 / Math.Sqrt(5) - 1E-12, -2 / Math.Sqrt(5) + 1E-12);
            Assert.InRange(turned.Direction.Y, 1 / Math.Sqrt(5) - 1E-12, 1 / Math.Sqrt(5) + 1E-12);
            Assert.True(turned.IsValid);
        }

        [Fact]
        public void AnUnevenScaling_TurnsTheLine_AndTheDirectionIsMadeUnitLengthAgain()
        {
            // Doubling X takes (1, 1) to (2, 1) and the direction (1, 1)/sqrt 2 to (2, 1), which is (2, 1)/sqrt 5 of unit length.
            var line = new GeoXLine2(P(1, 1), new GeoVector2(1, 1));
            GeoXLine2 stretched = line.TransformBy(GeoTransform2.Scaling(2, 1));

            AssertNear(P(2, 1), stretched.Origin, 1E-12);
            Assert.InRange(stretched.Direction.X, 2 / Math.Sqrt(5) - 1E-12, 2 / Math.Sqrt(5) + 1E-12);
            Assert.InRange(stretched.Direction.Y, 1 / Math.Sqrt(5) - 1E-12, 1 / Math.Sqrt(5) + 1E-12);
            Assert.Equal(line.TransformBy(GeoTransform2.Scaling(2, 1)), GeoTransform2.Scaling(2, 1).Transform(line));
        }

        [Fact]
        public void ATransformationCollapsingTheDirection_IsRefused_AndNothingIsRefusedToo()
        {
            GeoTransform2 squash = GeoTransform2.Scaling(0, 1);

            Assert.Throws<InvalidOperationException>(() => new GeoXLine2(P(5, 5), GeoVector2.XAxis).TransformBy(squash));
            Assert.Throws<InvalidOperationException>(() => squash.Transform(new GeoXLine2(P(5, 5), GeoVector2.XAxis)));
            Assert.Throws<ArgumentNullException>(() => new GeoXLine2(P(5, 5), GeoVector2.XAxis).TransformBy(null));

            GeoXLine2 upright = new GeoXLine2(P(5, 5), GeoVector2.YAxis).TransformBy(squash);

            AssertNear(P(0, 5), upright.Origin, 1E-12);
            Assert.Equal(1.0, upright.Direction.Y, 14);
        }

        #endregion

        #region Along the line

        [Fact]
        public void FiveAlongTheLineFromOneNoughtAlongThreeFour_IsFourFour_AndFiveBehindIsMinusTwoMinusFour()
        {
            var line = new GeoXLine2(P(1, 0), new GeoVector2(3, 4));

            AssertNear(P(4, 4), line.GetPointAtDistance(5), 1E-12);
            AssertNear(P(-2, -4), line.GetPointAtDistance(-5), 1E-12);
            Assert.Equal(5.0, line.GetDistanceAtPoint(P(4, 4)), 12);
            Assert.Equal(-5.0, line.GetDistanceAtPoint(P(-2, -4)), 12);
            Assert.True(line.IsPointOn(P(-2, -4), Strict));
        }

        [Fact]
        public void TheDistanceAtAPointOffTheLine_IsWhereItsFootFalls_BehindTheOriginToo()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.Equal(50.0, line.GetDistanceAtPoint(P(50, 40)), 12);
            Assert.Equal(-30.0, line.GetDistanceAtPoint(P(-30, 40)), 12);
        }

        [Fact]
        public void TheClosestPointToAPointBesideTheLine_IsItsFoot_BehindTheOriginAsAheadOfIt()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            AssertNear(P(50, 0), line.GetClosestPointOnBoundary(P(50, 40)), 1E-12);
            AssertNear(P(-30, 0), line.GetClosestPointOnBoundary(P(-30, 40)), 1E-12);
        }

        [Fact]
        public void ToLine_IsTheSegmentBetweenTwoSignedDistances_RunningBackwardsWhenTheSecondIsTheSmaller()
        {
            var line = new GeoXLine2(P(10, 20), GeoVector2.XAxis);
            GeoLine2 across = line.ToLine(-30, 70);
            GeoLine2 back = line.ToLine(70, -30);

            AssertNear(P(-20, 20), across.StartPoint, 1E-12);
            AssertNear(P(80, 20), across.EndPoint, 1E-12);
            Assert.Equal(100.0, across.Length, 12);
            Assert.Equal(across.StartPoint, back.EndPoint);
            Assert.Equal(across.EndPoint, back.StartPoint);
            Assert.Equal(0.0, line.ToLine(5, 5).Length);
        }

        [Fact]
        public void ToLine_RefusesADistanceThatIsNaNOrInfinite_ButNotANegativeOne()
        {
            var line = new GeoXLine2(P(1, 0), GeoVector2.XAxis);

            Assert.Throws<ArgumentOutOfRangeException>(() => line.ToLine(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.ToLine(1, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.ToLine(0, double.PositiveInfinity));
            Assert.Equal(1.0, line.ToLine(-1, 0).Length, 12);
        }

        #endregion

        #region Ray, segment and back

        [Fact]
        public void ToRay_IsTheHalfAheadOfTheOrigin_AndTheOtherHalfIsTheReversedLinesRay()
        {
            var line = new GeoXLine2(P(10, 20), new GeoVector2(1, 2));
            GeoRay2 ahead = line.ToRay();
            GeoRay2 behind = line.Reverse().ToRay();

            Assert.Equal(line.Origin, ahead.Origin);
            Assert.Equal(line.Direction, ahead.Direction);
            Assert.Equal(-line.Direction.X, behind.Direction.X);
            Assert.Equal(-line.Direction.Y, behind.Direction.Y);
        }

        [Fact]
        public void ARayBecomesALineAndBack_ToTheLastBit()
        {
            var ray = new GeoRay2(P(3, -7), new GeoVector2(1, 2));
            GeoXLine2 line = ray.ToXLine();

            Assert.Equal(ray.Origin, line.Origin);
            Assert.Equal(ray.Direction, line.Direction);
            Assert.Equal(ray, line.ToRay());
            Assert.Equal(line, line.ToRay().ToXLine());
        }

        [Fact]
        public void ALineWithNoDirection_HasNoRay_AndARayWithNoneHasNoLine()
        {
            Assert.Throws<ArgumentException>(() => default(GeoXLine2).ToRay());
            Assert.Throws<ArgumentException>(() => default(GeoRay2).ToXLine());
        }

        [Fact]
        public void ASegmentGivesTheLineThroughItsStartAlongTheWayItRuns()
        {
            GeoXLine2 along = new GeoLine2(P(0, 0), P(100, 0)).ToXLine();
            GeoXLine2 back = new GeoLine2(P(100, 0), P(0, 0)).ToXLine();

            Assert.Equal(P(0, 0), along.Origin);
            Assert.Equal(1.0, along.Direction.X, 15);
            Assert.Equal(P(100, 0), back.Origin);
            Assert.Equal(-1.0, back.Direction.X, 15);
            Assert.True(along.IsSameLineAs(back, Strict));
        }

        [Fact]
        public void ASegmentTooShortToHaveADirection_HasNoLine()
        {
            Assert.Throws<ArgumentException>(() => new GeoLine2(P(5, 5), P(5, 5)).ToXLine());
        }

        #endregion

        #region Clipping to a rectangle

        [Fact]
        public void TheLineAlongXThroughTheSquare_IsClippedToItsTwoSides()
        {
            // The square 100 by 100 with a corner at the origin; the line from (-50, 50) enters at (0, 50) and leaves at (100, 50).
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            GeoLine2? clipped = new GeoXLine2(P(-50, 50), GeoVector2.XAxis).ClipTo(square);

            Assert.NotNull(clipped);
            AssertNear(P(0, 50), clipped.Value.StartPoint, 1E-9);
            AssertNear(P(100, 50), clipped.Value.EndPoint, 1E-9);
        }

        [Fact]
        public void TheClippedSegment_RunsTheWayTheLineRuns_WhateverItsOrigin()
        {
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            GeoLine2? backwards = new GeoXLine2(P(500, 500), new GeoVector2(-1, -1)).ClipTo(square);
            GeoLine2? forwards = new GeoXLine2(P(50, 50), new GeoVector2(1, 1)).ClipTo(square);

            AssertNear(P(100, 100), backwards.Value.StartPoint, 1E-9);
            AssertNear(P(0, 0), backwards.Value.EndPoint, 1E-9);
            AssertNear(P(0, 0), forwards.Value.StartPoint, 1E-9);
            AssertNear(P(100, 100), forwards.Value.EndPoint, 1E-9);
        }

        [Fact]
        public void ARotatedRectangle_IsClippedInItsOwnFrame()
        {
            // 100 long and 20 wide, turned a quarter, it stands 20 wide in X and 100 tall in Y about the origin.
            var standing = new GeoRectangle2(P(0, 0), 100, 20, Math.PI / 2);
            GeoLine2? across = new GeoXLine2(P(-300, 0), GeoVector2.XAxis).ClipTo(standing);
            GeoLine2? up = new GeoXLine2(P(0, -300), GeoVector2.YAxis).ClipTo(standing);

            AssertNear(P(-10, 0), across.Value.StartPoint, 1E-9);
            AssertNear(P(10, 0), across.Value.EndPoint, 1E-9);
            AssertNear(P(0, -50), up.Value.StartPoint, 1E-9);
            AssertNear(P(0, 50), up.Value.EndPoint, 1E-9);
        }

        [Fact]
        public void ALineMissingTheRectangle_GivesNothing()
        {
            var square = new GeoRectangle2(P(50, 50), 100, 100);

            Assert.Null(new GeoXLine2(P(150, 0), GeoVector2.YAxis).ClipTo(square));
            Assert.Null(new GeoXLine2(P(0, 150), GeoVector2.XAxis).ClipTo(square));
            Assert.Null(new GeoXLine2(P(210, 0), new GeoVector2(-1, 1)).ClipTo(square, Strict));
        }

        [Fact]
        public void ALineAlongASide_IsClippedToTheSide_WithinThePointToleranceOfIt()
        {
            // Along the top side at y = 100: the side itself, a half thousandth off it, but not two thousandths off.
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            GeoLine2? onIt = new GeoXLine2(P(-20, 100), GeoVector2.XAxis).ClipTo(square, Strict);
            GeoLine2? nearIt = new GeoXLine2(P(-20, 100.0005), GeoVector2.XAxis).ClipTo(square, Strict);

            AssertNear(P(0, 100), onIt.Value.StartPoint, 1E-9);
            AssertNear(P(100, 100), onIt.Value.EndPoint, 1E-9);
            // A half thousandth off, it is clipped to the square grown by the thousandth.
            AssertNear(P(-0.001, 100.0005), nearIt.Value.StartPoint, 1E-9);
            AssertNear(P(100.001, 100.0005), nearIt.Value.EndPoint, 1E-9);
            Assert.Null(new GeoXLine2(P(-20, 100.002), GeoVector2.XAxis).ClipTo(square, Strict));
        }

        [Fact]
        public void ALineTouchingACorner_GivesASegmentOfNoMoreThanTheToleranceAbout_AndOneTwoThousandthsPastItGivesNothing()
        {
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            GeoLine2? touching = new GeoXLine2(P(100, 100), new GeoVector2(1, -1)).ClipTo(square, Strict);
            // x + y = 200.0028 is 0.00198 from the corner (100, 100).
            GeoLine2? past = new GeoXLine2(P(100.0014, 100.0014), new GeoVector2(1, -1)).ClipTo(square, Strict);

            // x + y = 200.00099 is 0.0007 from the corner: within the thousandth, so it still touches.
            GeoLine2? within = new GeoXLine2(P(100.000495, 100.000495), new GeoVector2(1, -1)).ClipTo(square, Strict);

            // Within the thousandth of the corner it is clipped to the corner of the square grown by it: a stretch of no more than
            // 0.0029 about the corner.
            Assert.NotNull(touching);
            Assert.True(touching.Value.Length <= 0.003);
            AssertNear(P(100, 100), touching.Value.StartPoint, 0.0015);
            Assert.NotNull(within);
            Assert.True(within.Value.Length <= 0.003);
            AssertNear(P(100, 100), within.Value.StartPoint, 0.0015);
            Assert.Null(past);
        }

        [Fact]
        public void ALineAThousandthAndTwoTenthsOffACorner_GivesNothing_NotASegmentInTheGrownSquare()
        {
            // 0.0012 from the corner (100, 100) is past the thousandth, though inside the corner of the square grown by it, which is
            // 0.0014 away.
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            double shift = 0.0012 / Math.Sqrt(2);

            Assert.Null(new GeoXLine2(P(100 + shift, 100 + shift), new GeoVector2(1, -1)).ClipTo(square, Strict));
            Assert.Null(new GeoXLine2(P(100 + shift, 100 + shift), new GeoVector2(1, -1)).ClipTo(square));
            Assert.NotNull(new GeoXLine2(P(100 + 0.0009 / Math.Sqrt(2), 100 + 0.0009 / Math.Sqrt(2)), new GeoVector2(1, -1)).ClipTo(square, Strict));
        }

        [Fact]
        public void TheFormWithoutATolerance_ClipsAsTheGlobalOneDoes()
        {
            var square = new GeoRectangle2(P(50, 50), 100, 100);
            var line = new GeoXLine2(P(-50, 50), new GeoVector2(1, 0.3));

            Assert.Equal(line.ClipTo(square, Tolerance.Global), line.ClipTo(square));
        }

        #endregion

        #region Perpendicular and offset

        [Fact]
        public void ThePerpendicularThroughAPoint_StartsAtThePointAndRunsAQuarterTurnLeft()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);
            GeoXLine2 across = line.GetPerpendicular(P(30, 40));

            Assert.Equal(P(30, 40), across.Origin);
            Assert.Equal(new GeoVector2(0, 1), across.Direction);
            Assert.True(across.Direction.IsPerpendicularTo(line.Direction));
            Assert.Equal(new GeoVector2(0, -1), line.Reverse().GetPerpendicular(P(30, 40)).Direction);
        }

        [Fact]
        public void OffsettingMovesTheLineToItsLeft_AndANegativeDistanceToItsRight()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            AssertNear(P(0, 5), line.Offset(5).Origin, 1E-12);
            AssertNear(P(0, -5), line.Offset(-5).Origin, 1E-12);
            Assert.Equal(line.Direction, line.Offset(5).Direction);
            Assert.Equal(5.0, line.DistanceTo(line.Offset(5).Origin), 12);
            AssertNear(P(0, -5), line.Reverse().Offset(5).Origin, 1E-12);
        }

        [Fact]
        public void OffsettingByNoNumber_IsRefused()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.Throws<ArgumentOutOfRangeException>(() => line.Offset(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Offset(double.PositiveInfinity));
            Assert.Equal(line, line.Offset(0));
        }

        #endregion

        #region Points, sides and parallels

        [Fact]
        public void APointOnTheLine_IsOnItAheadOfTheOriginAndBehindIt_AndTwoThousandthsBesideItIsNot()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.True(line.IsPointOn(P(100, 0), Strict));
            Assert.True(line.IsPointOn(P(-1, 0), Strict));
            Assert.True(line.IsPointOn(P(-5000, 0.0005), Strict));
            Assert.False(line.IsPointOn(P(5, 0.002), Strict));
            Assert.True(line.IsPointOn(P(5, 0.5), Loose));
            Assert.Equal(line.IsPointOn(P(5, 0.0005), Tolerance.Global), line.IsPointOn(P(5, 0.0005)));
        }

        [Fact]
        public void LocatingAPoint_SaysOnSideOrOutSideAndNeverInside()
        {
            var line = new GeoXLine2(P(10, 20), GeoVector2.XAxis);

            Assert.Equal(PointLocation.OnSide, line.Locate(P(50, 20), Strict));
            Assert.Equal(PointLocation.OnSide, line.Locate(P(-50, 20.0005), Strict));
            Assert.Equal(PointLocation.OutSide, line.Locate(P(50, 20.002), Strict));
            Assert.Equal(line.Locate(P(50, 25), Tolerance.Global), line.Locate(P(50, 25)));
        }

        [Fact]
        public void TheSideOfAPoint_IsSeenLookingAlongTheLine_AndTurnsWithItsDirection()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.Equal(LineSide.Left, line.GetSide(P(5, 3), Strict));
            Assert.Equal(LineSide.Right, line.GetSide(P(5, -3), Strict));
            Assert.Equal(LineSide.On, line.GetSide(P(5, 0.0005), Strict));
            Assert.Equal(LineSide.On, line.GetSide(P(-5000, 0), Strict));
            Assert.Equal(LineSide.Right, line.Reverse().GetSide(P(5, 3), Strict));
            Assert.Equal(line.GetSide(P(5, 3), Tolerance.Global), line.GetSide(P(5, 3)));
        }

        [Fact]
        public void LinesAtHalfADegree_AreParallel_AndAtTwoDegreesNot_EitherWayRound()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);
            double half = Math.PI / 360;
            double two = Math.PI / 90;

            Assert.True(line.IsParallelTo(new GeoXLine2(P(7, 7), new GeoVector2(Math.Cos(half), Math.Sin(half))), Strict));
            Assert.False(line.IsParallelTo(new GeoXLine2(P(7, 7), new GeoVector2(Math.Cos(two), Math.Sin(two))), Strict));
            Assert.True(line.IsParallelTo(line.Reverse(), Strict));
            Assert.True(line.IsParallelTo(new GeoLine2(P(0, 10), P(-100, 10)), Strict));
            Assert.False(line.IsParallelTo(new GeoLine2(P(0, 10), P(0, 100)), Strict));
            Assert.Equal(line.IsParallelTo(line.Reverse(), Tolerance.Global), line.IsParallelTo(line.Reverse()));
            Assert.Equal(line.IsParallelTo(new GeoLine2(P(0, 10), P(-100, 10)), Tolerance.Global), line.IsParallelTo(new GeoLine2(P(0, 10), P(-100, 10))));
        }

        [Fact]
        public void TheSameLineFromAnotherOriginAndTheOtherWay_IsTheSameLine_ButNotOneAThousandthsOffOrAtAnAngle()
        {
            var line = new GeoXLine2(P(0, 0), GeoVector2.XAxis);

            Assert.True(line.IsSameLineAs(new GeoXLine2(P(500, 0), new GeoVector2(-1, 0)), Strict));
            Assert.True(line.IsSameLineAs(new GeoXLine2(P(-20, 0.0004), GeoVector2.XAxis), Strict));
            Assert.False(line.IsSameLineAs(new GeoXLine2(P(0, 0.002), GeoVector2.XAxis), Strict));
            Assert.False(line.IsSameLineAs(new GeoXLine2(P(0, 0), new GeoVector2(1, 0.01)), Strict));
            Assert.True(line.IsSameLineAs(new GeoXLine2(P(500, 0), new GeoVector2(-1, 0)), Strict) == new GeoXLine2(P(500, 0), new GeoVector2(-1, 0)).IsSameLineAs(line, Strict));
            Assert.Equal(line.IsSameLineAs(line.Reverse(), Tolerance.Global), line.IsSameLineAs(line.Reverse()));
        }

        #endregion

        #region The guide's example

        [Fact]
        public void TheExampleOfThePlaneGuide_GivesTheNumbersItPrints()
        {
            var square = new GeoPolygon2(new[] { P(0, 0), P(100, 0), P(100, 100), P(0, 100) });
            var line = new GeoXLine2(P(-50, 50), GeoVector2.XAxis);
            GeoPoint2[] crossings = line.GetIntersections(square, Strict);

            Assert.Equal(2, crossings.Length);
            // In the order the guide prints them: edge by edge, as for a segment.
            AssertNear(P(100, 50), crossings[0], 1E-9);
            AssertNear(P(0, 50), crossings[1], 1E-9);
            Assert.True(line.CollidesWith(square, Strict));
            Assert.Equal(50.0, line.Offset(100).DistanceTo(square, Strict), 9);
            AssertNear(P(-80, 50), line.GetPointAtDistance(-30), 1E-12);
            GeoLine2? clipped = line.ClipTo(new GeoRectangle2(P(50, 50), 100, 100), Strict);
            AssertNear(P(0, 50), clipped.Value.StartPoint, 1E-9);
            AssertNear(P(100, 50), clipped.Value.EndPoint, 1E-9);
            Assert.True(line.IsPointOn(P(-5000, 50.0005), Strict));

            // The cut it is asked as: the segment from (-1, 50) to (101, 50).
            Assert.Equal(line.DistanceTo(square, Strict), new GeoLine2(P(-1, 50), P(101, 50)).DistanceTo(square));
        }

        #endregion

        #region Equality and text

        [Fact]
        public void EqualityReadsTheFields_AndTheHashFollowsIt()
        {
            var a = new GeoXLine2(P(1, 2), new GeoVector2(3, 4));
            var b = new GeoXLine2(P(1, 2), new GeoVector2(3, 4));

            Assert.True(a == b);
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.False(a == new GeoXLine2(P(1, 2.0001), new GeoVector2(3, 4)));
            Assert.False(a.Equals("not a line"));
        }

        [Fact]
        public void EqualWithinATolerance_ReadsTheOriginByThePointToleranceAndTheDirectionByTheVectorOne()
        {
            var a = new GeoXLine2(P(1, 2), GeoVector2.XAxis);

            Assert.True(a.IsEqualTo(new GeoXLine2(P(1.0005, 2), GeoVector2.XAxis), Strict));
            Assert.False(a.IsEqualTo(new GeoXLine2(P(1.002, 2), GeoVector2.XAxis), Strict));
            Assert.False(a.IsEqualTo(new GeoXLine2(P(1, 2), new GeoVector2(1, 0.001)), Strict));
            Assert.Equal(a.IsEqualTo(new GeoXLine2(P(1.0005, 2), GeoVector2.XAxis), Tolerance.Global), a.IsEqualTo(new GeoXLine2(P(1.0005, 2), GeoVector2.XAxis)));
        }

        [Fact]
        public void AConstructedLineIsValid_AndOneWithANaNOriginIsNot()
        {
            Assert.True(new GeoXLine2(P(1, 2), GeoVector2.XAxis).IsValid);
            Assert.False(new GeoXLine2(P(double.NaN, 2), GeoVector2.XAxis).IsValid);
        }

        [Fact]
        public void ItPrintsItsOriginAndItsDirection()
        {
            string text = new GeoXLine2(P(1, 2), GeoVector2.XAxis).ToString();

            Assert.StartsWith("XLine2(Origin: ", text);
            Assert.Contains(", Direction: ", text);
        }

        #endregion
    }
}
