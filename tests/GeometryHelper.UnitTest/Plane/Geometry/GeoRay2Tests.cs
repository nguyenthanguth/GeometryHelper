using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ray in the plane as a value: how it is made, moved, measured along, compared and printed, and which points
    /// are on it. It mirrors <see cref="GeoRay3"/> member for member, so every expectation here is the 3D ray's read in
    /// the plane, worked out by hand.
    /// </summary>
    /// <remarks>
    /// The tolerances are a thousandth for points and 1E-5 for vectors, as the default, and 1 for points where a test
    /// shows the tolerance is read. Offsets of 0.0005 and 0.002 sit either side of the thousandth, a factor of two clear
    /// of it, so no answer here hangs on rounding at the boundary.
    /// </remarks>
    public class GeoRay2Tests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static void AssertNear(GeoPoint2 expected, GeoPoint2 actual, double within)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertNear(GeoVector2 expected, GeoVector2 actual, double within)
        {
            Assert.InRange(actual.X - expected.X, -within, within);
            Assert.InRange(actual.Y - expected.Y, -within, within);
        }

        #region Making one

        [Fact]
        public void TheDirectionIsMadeUnitLength_NineAlongYIsOneAlongY()
        {
            var ray = new GeoRay2(P(1, 2), new GeoVector2(0, 9));

            Assert.Equal(P(1, 2), ray.Origin);
            Assert.Equal(new GeoVector2(0, 1), ray.Direction);
        }

        [Fact]
        public void ADirectionOfThreeAndFour_IsSixAndEightTenths()
        {
            // Length 5, so (0.6, 0.8); the length of the result is one to the last digit or two.
            var ray = new GeoRay2(P(0, 0), new GeoVector2(3, 4));

            AssertNear(new GeoVector2(0.6, 0.8), ray.Direction, 1E-15);
            Assert.InRange(ray.Direction.Length, 1.0 - 1E-15, 1.0 + 1E-15);
        }

        [Fact]
        public void ARayThroughAPoint_AimsAtThatPoint()
        {
            // From (1, 1) through (4, 5): along (3, 4), so (0.6, 0.8), and (4, 5) is 5 along it.
            var ray = new GeoRay2(P(1, 1), P(4, 5));

            AssertNear(new GeoVector2(0.6, 0.8), ray.Direction, 1E-15);
            Assert.True(ray.IsPointOn(P(4, 5), Strict));
            Assert.Equal(5.0, ray.GetDistanceAtPoint(P(4, 5)), 12);
        }

        [Fact]
        public void ADirectionOfNoLength_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoRay2(P(1, 2), GeoVector2.Zero));
            Assert.Throws<ArgumentException>(() => new GeoRay2(P(1, 2), P(1, 2)));
        }

        #endregion

        #region Copies, reversal and motion

        [Fact]
        public void ACopyAndATwiceReversedRay_AreTheOriginalToTheLastBit()
        {
            // (1, 2) normalized is a direction that normalizing again moves in its last digits (0.447213595499957 93
            // becomes ...58), so a copy or a reversal that went through the normalizing constructor would come back
            // unequal; GeoRay3 keeps them exact through its private constructor, and so must this.
            var ray = new GeoRay2(P(3, -7), new GeoVector2(1, 2));

            GeoRay2 copy = ray.Clone();
            GeoRay2 twice = ray.Reverse().Reverse();

            Assert.Equal(ray, copy);
            Assert.True(ray == copy);
            Assert.Equal(ray, twice);
            Assert.Equal(ray.Direction.X, twice.Direction.X);
            Assert.Equal(ray.Direction.Y, twice.Direction.Y);
        }

        [Fact]
        public void TheReversedRay_KeepsItsOriginAndPointsExactlyTheOtherWay()
        {
            var ray = new GeoRay2(P(3, -7), new GeoVector2(1, 2));
            GeoRay2 reversed = ray.Reverse();

            Assert.Equal(ray.Origin, reversed.Origin);
            Assert.Equal(-ray.Direction.X, reversed.Direction.X);
            Assert.Equal(-ray.Direction.Y, reversed.Direction.Y);
        }

        [Fact]
        public void TranslatingMovesTheOriginAndKeepsTheDirectionToTheLastBit()
        {
            // A8: Translate goes through the already-normalized constructor, so (1, 2)/√5, which normalizing again would
            // move in its last digits, comes through unchanged.
            var ray = new GeoRay2(P(3, -7), new GeoVector2(1, 2));
            GeoRay2 moved = ray.Translate(new GeoVector2(10, 20));

            Assert.Equal(P(13, 13), moved.Origin);
            Assert.Equal(ray.Direction.X, moved.Direction.X);
            Assert.Equal(ray.Direction.Y, moved.Direction.Y);
        }

        [Fact]
        public void ADefaultRay_IsMovedCopiedAndReversedWithoutThrowing()
        {
            // A8 and D13: it has no direction to normalize, and none is asked of it.
            GeoRay2 moved = default(GeoRay2).Translate(new GeoVector2(10, 20));

            Assert.Equal(P(10, 20), moved.Origin);
            Assert.Equal(GeoVector2.Zero, moved.Direction);
            Assert.Equal(default(GeoRay2), default(GeoRay2).Clone());
            Assert.Equal(P(0, 0), default(GeoRay2).Reverse().Origin);
        }

        [Fact]
        public void AQuarterTurnAboutTheOrigin_TurnsTheOriginAndTheDirection()
        {
            // (10, 0) goes to (0, 10); the direction (1, 2)/√5 goes to (-2, 1)/√5.
            var ray = new GeoRay2(P(10, 0), new GeoVector2(1, 2));
            GeoRay2 turned = ray.TransformBy(GeoTransform2.Rotation(Math.PI / 2));

            AssertNear(P(0, 10), turned.Origin, 1E-12);
            AssertNear(new GeoVector2(-2 / Math.Sqrt(5), 1 / Math.Sqrt(5)), turned.Direction, 1E-12);
            Assert.True(turned.IsValid);
        }

        [Fact]
        public void AnUnevenScaling_IsAllowedAndTheDirectionIsMadeUnitLengthAgain()
        {
            // Doubling X takes (1, 1) to (2, 1) and the direction (1, 1)/√2 to (2, 1), which is (2, 1)/√5 of unit length.
            var ray = new GeoRay2(P(1, 1), new GeoVector2(1, 1));
            GeoRay2 stretched = ray.TransformBy(GeoTransform2.Scaling(2, 1));

            AssertNear(P(2, 1), stretched.Origin, 1E-12);
            AssertNear(new GeoVector2(2 / Math.Sqrt(5), 1 / Math.Sqrt(5)), stretched.Direction, 1E-12);
            Assert.InRange(stretched.Direction.Length, 1.0 - 1E-12, 1.0 + 1E-12);
        }

        [Fact]
        public void TheTransformationItselfGivesTheSameRayAsTransformBy()
        {
            var ray = new GeoRay2(P(1, 1), new GeoVector2(1, 1));
            GeoTransform2 stretch = GeoTransform2.Scaling(2, 1);

            Assert.Equal(ray.TransformBy(stretch), stretch.Transform(ray));
        }

        [Fact]
        public void ATransformationCollapsingTheDirection_IsRefused()
        {
            // Squashing X to nothing leaves a ray along X no direction; one along Y keeps its own.
            GeoTransform2 squash = GeoTransform2.Scaling(0, 1);

            Assert.Throws<InvalidOperationException>(() => new GeoRay2(P(5, 5), GeoVector2.XAxis).TransformBy(squash));
            Assert.Throws<InvalidOperationException>(() => squash.Transform(new GeoRay2(P(5, 5), GeoVector2.XAxis)));

            GeoRay2 upright = new GeoRay2(P(5, 5), GeoVector2.YAxis).TransformBy(squash);

            AssertNear(P(0, 5), upright.Origin, 1E-12);
            AssertNear(GeoVector2.YAxis, upright.Direction, 1E-15);
        }

        [Fact]
        public void TransformingByNothing_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new GeoRay2(P(5, 5), GeoVector2.XAxis).TransformBy(null));
        }

        #endregion

        #region Along the ray

        [Fact]
        public void FiveAlongTheRayFromOneNoughtAlongThreeFour_IsFourFour()
        {
            var ray = new GeoRay2(P(1, 0), new GeoVector2(3, 4));

            AssertNear(P(4, 4), ray.GetPointAtDistance(5), 1E-12);
            Assert.Equal(5.0, ray.GetDistanceAtPoint(P(4, 4)), 12);
        }

        [Fact]
        public void ANegativeDistance_ExtrapolatesBehindTheOriginAndOffTheRay()
        {
            // Five back from (1, 0) along (0.6, 0.8) is (-2, -4); it is on the carrier line, five behind the origin.
            var ray = new GeoRay2(P(1, 0), new GeoVector2(3, 4));
            GeoPoint2 behind = ray.GetPointAtDistance(-5);

            AssertNear(P(-2, -4), behind, 1E-12);
            Assert.Equal(-5.0, ray.GetDistanceAtPoint(behind), 12);
            Assert.False(ray.IsPointOn(behind, Strict));
        }

        [Fact]
        public void TheDistanceAtAPointOffTheRay_IsWhereItsFootFalls()
        {
            // (5, 3) has its foot at (5, 0) on the ray from the origin along X; (-4, 3) its foot four behind.
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);

            Assert.Equal(5.0, ray.GetDistanceAtPoint(P(5, 3)), 12);
            Assert.Equal(-4.0, ray.GetDistanceAtPoint(P(-4, 3)), 12);
        }

        [Fact]
        public void ToLine_IsTheSegmentFromTheOriginOutToTheDistanceGiven()
        {
            var ray = new GeoRay2(P(1, 0), new GeoVector2(3, 4));
            GeoLine2 line = ray.ToLine(10);

            Assert.Equal(P(1, 0), line.StartPoint);
            AssertNear(P(7, 8), line.EndPoint, 1E-12);
            Assert.Equal(10.0, line.Length, 12);
            Assert.Equal(0.0, ray.ToLine(0).Length);
        }

        [Fact]
        public void ToLine_RefusesADistanceThatIsNegativeNaNOrInfinite()
        {
            var ray = new GeoRay2(P(1, 0), new GeoVector2(3, 4));

            Assert.Throws<ArgumentOutOfRangeException>(() => ray.ToLine(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ray.ToLine(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ray.ToLine(double.PositiveInfinity));
        }

        [Fact]
        public void TheClosestPointToAPointBesideTheRay_IsItsFoot_AndBehindTheOriginItIsTheOrigin()
        {
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);

            AssertNear(P(5, 0), ray.GetClosestPointOnBoundary(P(5, 3)), 1E-12);
            Assert.Equal(P(0, 0), ray.GetClosestPointOnBoundary(P(-4, 3)));
        }

        #endregion

        #region Points on the ray

        [Fact]
        public void APointFiveTenThousandthsBehindTheOrigin_IsOnTheRay_AndTwoThousandthsBehindItIsNot()
        {
            // D2: on the ray is within the point tolerance of it; behind the origin by more than that is off it.
            var ray = new GeoRay2(P(10, 20), GeoVector2.XAxis);

            Assert.True(ray.IsPointOn(P(10 - 0.0005, 20), Strict));
            Assert.False(ray.IsPointOn(P(10 - 0.002, 20), Strict));
            Assert.True(Containment2.IsPointOn(ray, P(10 - 0.0005, 20), Strict));
            Assert.False(Containment2.IsPointOn(ray, P(10 - 0.002, 20), Strict));
        }

        [Fact]
        public void APointFiveTenThousandthsBesideTheRayTenThousandAlong_IsOnIt_AndTwoThousandthsBesideItIsNot()
        {
            var ray = new GeoRay2(P(10, 20), GeoVector2.XAxis);

            Assert.True(ray.IsPointOn(P(10010, 20.0005), Strict));
            Assert.False(ray.IsPointOn(P(10010, 20.002), Strict));
        }

        [Fact]
        public void APointHalfBehindTheOrigin_IsOnTheRayWithinAToleranceOfOne()
        {
            var ray = new GeoRay2(P(10, 20), GeoVector2.XAxis);

            Assert.False(ray.IsPointOn(P(9.5, 20), Strict));
            Assert.True(ray.IsPointOn(P(9.5, 20), Loose));
        }

        [Fact]
        public void LocatingAPoint_SaysOnSideOrOutSideAndNeverInside()
        {
            // A ray encloses nothing, as GeoRay3.Locate says.
            var ray = new GeoRay2(P(10, 20), GeoVector2.XAxis);

            Assert.Equal(PointLocation.OnSide, ray.Locate(P(50, 20), Strict));
            Assert.Equal(PointLocation.OnSide, ray.Locate(P(10 - 0.0005, 20), Strict));
            Assert.Equal(PointLocation.OutSide, ray.Locate(P(10 - 0.002, 20), Strict));
            Assert.Equal(PointLocation.OutSide, ray.Locate(P(50, 25), Strict));
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(ray, P(50, 20), Strict));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(ray, P(50, 25), Strict));
        }

        [Fact]
        public void TheFormsWithoutATolerance_AnswerAsTheGlobalToleranceDoes()
        {
            var ray = new GeoRay2(P(10, 20), GeoVector2.XAxis);

            foreach (GeoPoint2 point in new[] { P(50, 20), P(9.9995, 20), P(9.998, 20), P(50, 25) })
            {
                Assert.Equal(ray.IsPointOn(point, Tolerance.Global), ray.IsPointOn(point));
                Assert.Equal(ray.Locate(point, Tolerance.Global), ray.Locate(point));
                Assert.Equal(Containment2.IsPointOn(ray, point, Tolerance.Global), Containment2.IsPointOn(ray, point));
                Assert.Equal(Containment2.Locate(ray, point, Tolerance.Global), Containment2.Locate(ray, point));
            }
        }

        #endregion

        #region Parallel

        [Fact]
        public void ASegmentAtHalfADegree_IsParallel_AndOneAtTwoDegreesIsNot()
        {
            // The angle tolerance is a degree; either way round a segment along the ray is parallel to it.
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            double half = Math.PI / 360;
            double two = Math.PI / 90;

            Assert.True(ray.IsParallelTo(new GeoLine2(P(0, 0), P(100 * Math.Cos(half), 100 * Math.Sin(half))), Strict));
            Assert.False(ray.IsParallelTo(new GeoLine2(P(0, 0), P(100 * Math.Cos(two), 100 * Math.Sin(two))), Strict));
            Assert.True(ray.IsParallelTo(new GeoLine2(P(300, 40), P(200, 40)), Strict));
            Assert.Equal(ray.IsParallelTo(new GeoLine2(P(300, 40), P(200, 40)), Tolerance.Global), ray.IsParallelTo(new GeoLine2(P(300, 40), P(200, 40))));
        }

        [Fact]
        public void ASegmentOfNoLength_IsNotParallelToTheRay()
        {
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);

            Assert.False(ray.IsParallelTo(new GeoLine2(P(5, 5), P(5, 5)), Strict));
        }

        #endregion

        #region Equality and printing

        [Fact]
        public void TwoRaysMadeAlike_AreEqualAndHashAlike_AndOneMovedIsNot()
        {
            var ray = new GeoRay2(P(3, -7), new GeoVector2(1, 2));
            var same = new GeoRay2(P(3, -7), new GeoVector2(1, 2));
            var moved = new GeoRay2(P(3, -6), new GeoVector2(1, 2));

            Assert.True(ray.Equals(same));
            Assert.True(ray.Equals((object)same));
            Assert.True(ray == same);
            Assert.False(ray != same);
            Assert.Equal(ray.GetHashCode(), same.GetHashCode());
            Assert.False(ray.Equals(moved));
            Assert.True(ray != moved);
            Assert.False(ray.Equals((object)"a ray"));
        }

        [Fact]
        public void TwoRaysFiveTenThousandthsApart_AreEqualWithinAThousandth_AndTwoThousandthsApartAreNot()
        {
            var ray = new GeoRay2(P(3, -7), GeoVector2.XAxis);

            Assert.True(ray.IsEqualTo(new GeoRay2(P(3.0005, -7), GeoVector2.XAxis), Strict));
            Assert.False(ray.IsEqualTo(new GeoRay2(P(3.002, -7), GeoVector2.XAxis), Strict));
            Assert.Equal(ray.IsEqualTo(new GeoRay2(P(3.0005, -7), GeoVector2.XAxis), Tolerance.Global), ray.IsEqualTo(new GeoRay2(P(3.0005, -7), GeoVector2.XAxis)));
        }

        [Fact]
        public void TwoRaysTurnedATenMillionthApart_AreEqual_AndAThousandthApartAreNot()
        {
            // The vector tolerance is 1E-5: a turn of 1E-7 radians moves the unit direction by 1E-7, a turn of 1E-3 by 1E-3.
            var ray = new GeoRay2(P(3, -7), GeoVector2.XAxis);

            Assert.True(ray.IsEqualTo(new GeoRay2(P(3, -7), new GeoVector2(Math.Cos(1E-7), Math.Sin(1E-7))), Strict));
            Assert.False(ray.IsEqualTo(new GeoRay2(P(3, -7), new GeoVector2(Math.Cos(1E-3), Math.Sin(1E-3))), Strict));
            Assert.False(ray.IsEqualTo(ray.Reverse(), Strict));
        }

        [Fact]
        public void ARayPrintsItsOriginAndDirection()
        {
            Assert.Equal("Ray2(Origin: (1, 2), Direction: (0, 1))", new GeoRay2(P(1, 2), new GeoVector2(0, 5)).ToString());
        }

        [Fact]
        public void AMadeRayIsValid_AndADefaultOneOrOneFromANaNIsNot()
        {
            Assert.True(new GeoRay2(P(1, 2), GeoVector2.XAxis).IsValid);
            Assert.False(default(GeoRay2).IsValid);
            Assert.False(new GeoRay2(P(double.NaN, 2), GeoVector2.XAxis).IsValid);
        }

        #endregion

        #region A default ray and a NaN (D13)

        [Fact]
        public void ADefaultRay_IsTheOriginWithNoDirection_AndPrintsSo()
        {
            GeoRay2 ray = default(GeoRay2);

            Assert.Equal(P(0, 0), ray.Origin);
            Assert.Equal(GeoVector2.Zero, ray.Direction);
            Assert.Equal("Ray2(Origin: (0, 0), Direction: (0, 0))", ray.ToString());
        }

        [Fact]
        public void ADefaultRay_IsReadAsItsOrigin_ByThePointQuestions()
        {
            // With no direction every point's foot falls at the origin: (3, 4) is 5 away, its shortest line runs from the
            // origin to it, and the point at any distance along is the origin.
            GeoRay2 ray = default(GeoRay2);

            Assert.Equal(5.0, ray.DistanceTo(P(3, 4)), 12);
            Assert.Equal(0.0, ray.GetDistanceAtPoint(P(3, 4)));
            Assert.Equal(P(0, 0), ray.GetPointAtDistance(7));
            Assert.Equal(P(0, 0), ray.GetClosestPointOnBoundary(P(3, 4)));
            Assert.Equal(P(0, 0), ray.GetShortestLineTo(P(3, 4), Strict).StartPoint);
            Assert.Equal(P(3, 4), ray.GetShortestLineTo(P(3, 4), Strict).EndPoint);
            Assert.True(ray.IsPointOn(P(0, 0.0005), Strict));
            Assert.False(ray.IsPointOn(P(0, 0.002), Strict));
        }

        [Fact]
        public void ADefaultRay_AnswersEveryQueryAboutEveryShapeWithoutThrowing()
        {
            // D13: nothing throws, and a ray of no direction answers as the point of its origin.
            GeoRay2 ray = default(GeoRay2);
            var square = new GeoPolygon2(P(10, 10), P(20, 10), P(20, 20), P(10, 20));

            Assert.Equal(Math.Sqrt(200), ray.DistanceTo(square, Strict), 12);
            Assert.Equal(40.0, ray.DistanceTo(new GeoCircle2(P(30, 40), 10), Strict), 9);
            Assert.False(ray.CollidesWith(square, Strict));
            Assert.Empty(ray.GetIntersections(square, Strict));
            Assert.Equal(P(0, 0), ray.GetShortestLineTo(square, Strict).StartPoint);
            AskEverything(ray);
        }

        [Fact]
        public void ARayFromANaN_IsCarriedThrough_ItsDistanceToAPointIsNaN_AndNothingThrows()
        {
            var ray = new GeoRay2(P(double.NaN, 0), GeoVector2.XAxis);

            Assert.True(double.IsNaN(ray.DistanceTo(P(3, 4))));
            AskEverything(ray);
        }

        /// <summary>
        /// Asks every query of D4, in both forms, about one shape of each kind, and every query of D5 back; an exception
        /// fails the test.
        /// </summary>
        private static void AskEverything(GeoRay2 ray)
        {
            var square = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));
            var hole = new GeoPolygon2(P(30, 30), P(70, 30), P(70, 70), P(30, 70));
            var face = new GeoFace2(square, new[] { hole });
            var rectangle = new GeoRectangle2(P(50, 50), 100, 100);
            var triangle = new GeoTriangle2(P(0, 0), P(100, 0), P(0, 100));
            var circle = new GeoCircle2(P(0, 0), 50);
            var arc = new GeoArc2(P(0, 0), 50, Math.PI / 6, Math.PI);
            var edge = new GeoEdge2(arc);
            var segment = new GeoLine2(P(50, -10), P(50, 10));
            var polyline = new GeoPolyline2(P(0, 0), P(100, 0), P(100, 100));
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(100, 0), P(100, 100) }, new[] { 0.0, 1.0 });
            var loop = new GeoPolygonArc2(new[] { P(0, 0), P(200, 0), P(200, 50), P(0, 50) }, new[] { 0.0, 1.0, 0.0, 1.0 });
            var other = new GeoRay2(P(50, -50), GeoVector2.YAxis);
            var ellipse = new GeoEllipse2(P(0, 0), GeoVector2.XAxis, 100, 50);
            var point = P(3, 4);

            ray.DistanceTo(point);
            ray.GetShortestLineTo(point);
            ray.GetShortestLineTo(point, Strict);
            point.DistanceTo(ray);
            point.GetShortestLineTo(ray);
            point.GetShortestLineTo(ray, Strict);

            foreach (Tolerance tolerance in new[] { Strict, Tolerance.Global })
            {
                ray.CollidesWith(arc, tolerance); ray.DistanceTo(arc, tolerance); ray.GetIntersections(arc, tolerance); ray.TryIntersectWith(arc, out _, tolerance); ray.GetShortestLineTo(arc, tolerance);
                ray.CollidesWith(circle, tolerance); ray.DistanceTo(circle, tolerance); ray.GetIntersections(circle, tolerance); ray.TryIntersectWith(circle, out _, tolerance); ray.GetShortestLineTo(circle, tolerance);
                ray.CollidesWith(edge, tolerance); ray.DistanceTo(edge, tolerance); ray.GetIntersections(edge, tolerance); ray.GetShortestLineTo(edge, tolerance);
                ray.CollidesWith(face, tolerance); ray.DistanceTo(face, tolerance); ray.GetIntersections(face, tolerance); ray.TryIntersectWith(face, out _, tolerance); ray.GetShortestLineTo(face, tolerance);
                ray.CollidesWith(segment, tolerance); ray.DistanceTo(segment, tolerance); ray.TryIntersectWith(segment, out GeoPoint2 _, tolerance); ray.GetShortestLineTo(segment, tolerance);
                ray.CollidesWith(square, tolerance); ray.DistanceTo(square, tolerance); ray.GetIntersections(square, tolerance); ray.GetShortestLineTo(square, tolerance);
                ray.CollidesWith(loop, tolerance); ray.DistanceTo(loop, tolerance); ray.GetIntersections(loop, tolerance); ray.TryIntersectWith(loop, out _, tolerance); ray.GetShortestLineTo(loop, tolerance);
                ray.CollidesWith(polyline, tolerance); ray.DistanceTo(polyline, tolerance); ray.GetIntersections(polyline, tolerance); ray.TryIntersectWith(polyline, out _, tolerance); ray.GetShortestLineTo(polyline, tolerance);
                ray.CollidesWith(chain, tolerance); ray.DistanceTo(chain, tolerance); ray.GetIntersections(chain, tolerance); ray.TryIntersectWith(chain, out _, tolerance); ray.GetShortestLineTo(chain, tolerance);
                ray.CollidesWith(rectangle, tolerance); ray.DistanceTo(rectangle, tolerance); ray.GetIntersections(rectangle, tolerance); ray.TryIntersectWith(rectangle, out _, tolerance); ray.GetShortestLineTo(rectangle, tolerance);
                ray.CollidesWith(triangle, tolerance); ray.DistanceTo(triangle, tolerance); ray.GetIntersections(triangle, tolerance); ray.TryIntersectWith(triangle, out _, tolerance); ray.GetShortestLineTo(triangle, tolerance);
                ray.CollidesWith(other, tolerance); ray.DistanceTo(other, tolerance); ray.TryIntersectWith(other, out GeoPoint2 _, tolerance); ray.GetShortestLineTo(other, tolerance);
                other.CollidesWith(ray, tolerance); other.DistanceTo(ray, tolerance); other.TryIntersectWith(ray, out GeoPoint2 _, tolerance); other.GetShortestLineTo(ray, tolerance);

                arc.CollidesWith(ray, tolerance); arc.DistanceTo(ray, tolerance); arc.GetIntersections(ray, tolerance); arc.TryIntersectWith(ray, out _, tolerance); arc.GetShortestLineTo(ray, tolerance);
                circle.CollidesWith(ray, tolerance); circle.DistanceTo(ray, tolerance); circle.GetIntersections(ray, tolerance); circle.TryIntersectWith(ray, out _, tolerance); circle.GetShortestLineTo(ray, tolerance);
                edge.CollidesWith(ray, tolerance); edge.DistanceTo(ray, tolerance); edge.GetIntersections(ray, tolerance); edge.GetShortestLineTo(ray, tolerance);
                ellipse.CollidesWith(ray, tolerance); ellipse.DistanceTo(ray, tolerance); ellipse.GetIntersections(ray, tolerance); ellipse.TryIntersectWith(ray, out _, tolerance); ellipse.GetShortestLineTo(ray, tolerance);
                face.CollidesWith(ray, tolerance); face.DistanceTo(ray, tolerance); face.GetIntersections(ray, tolerance); face.TryIntersectWith(ray, out _, tolerance); face.GetShortestLineTo(ray, tolerance);
                square.CollidesWith(ray, tolerance); square.DistanceTo(ray, tolerance); square.GetIntersections(ray, tolerance); square.GetShortestLineTo(ray, tolerance);
                loop.CollidesWith(ray, tolerance); loop.DistanceTo(ray, tolerance); loop.GetIntersections(ray, tolerance); loop.TryIntersectWith(ray, out _, tolerance); loop.GetShortestLineTo(ray, tolerance);
                polyline.CollidesWith(ray, tolerance); polyline.DistanceTo(ray, tolerance); polyline.GetIntersections(ray, tolerance); polyline.TryIntersectWith(ray, out _, tolerance); polyline.GetShortestLineTo(ray, tolerance);
                chain.CollidesWith(ray, tolerance); chain.DistanceTo(ray, tolerance); chain.GetIntersections(ray, tolerance); chain.TryIntersectWith(ray, out _, tolerance); chain.GetShortestLineTo(ray, tolerance);
                rectangle.CollidesWith(ray, tolerance); rectangle.DistanceTo(ray, tolerance); rectangle.GetIntersections(ray, tolerance); rectangle.TryIntersectWith(ray, out _, tolerance); rectangle.GetShortestLineTo(ray, tolerance);
                triangle.CollidesWith(ray, tolerance); triangle.DistanceTo(ray, tolerance); triangle.GetIntersections(ray, tolerance); triangle.TryIntersectWith(ray, out _, tolerance); triangle.GetShortestLineTo(ray, tolerance);
            }
        }

        #endregion
    }
}
