using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Lengthening, extending, trimming and cornering line segments (Lengthen2 and the GeoLine2 members that
    /// delegate to it), checked against results worked out by hand and against the properties every such
    /// operation must keep: the segment stays on its own line, the end that is not named never moves, and a
    /// rigid motion of the whole scene moves the answer with it.
    /// </summary>
    public class LengthenTests
    {
        private static readonly GeoLine2 Horizontal = new GeoLine2(0.0, 0.0, 10.0, 0.0);

        private static void AssertPoint(GeoPoint2 expected, GeoPoint2 actual, double tolerance = 1e-9)
        {
            Assert.True(expected.DistanceTo(actual) <= tolerance, $"expected {expected}, got {actual}");
        }

        private static void AssertLine(GeoLine2 expected, GeoLine2 actual, double tolerance = 1e-9)
        {
            AssertPoint(expected.StartPoint, actual.StartPoint, tolerance);
            AssertPoint(expected.EndPoint, actual.EndPoint, tolerance);
        }

        #region By distance

        [Fact]
        public void Extend_OneEnd_MovesOnlyThatEndAlongTheLine()
        {
            AssertLine(new GeoLine2(0, 0, 15, 0), Horizontal.Extend(5.0, LineEnd.End));
            AssertLine(new GeoLine2(-5, 0, 10, 0), Horizontal.Extend(5.0, LineEnd.Start));

            // A negative distance shortens.
            AssertLine(new GeoLine2(0, 0, 7, 0), Horizontal.Extend(-3.0, LineEnd.End));
            AssertLine(new GeoLine2(3, 0, 10, 0), Horizontal.Extend(-3.0, LineEnd.Start));
        }

        [Fact]
        public void Extend_BothEnds_FollowsAnObliqueDirection()
        {
            var line = new GeoLine2(0, 0, 3, 4);

            AssertLine(new GeoLine2(-3, -4, 6, 8), line.Extend(5.0, 5.0));
            AssertLine(new GeoLine2(0.6, 0.8, 3, 4), line.Extend(-1.0, 0.0));
            Assert.Equal(15.0, line.Extend(5.0, 5.0).Length, 12);
        }

        [Fact]
        public void Extend_ThroughCoreAndInstance_Agree()
        {
            var line = new GeoLine2(1, 2, 7, -3);

            Assert.Equal(Lengthen2.Extend(line, 2.5, LineEnd.End), line.Extend(2.5, LineEnd.End));
            Assert.Equal(Lengthen2.Extend(line, 2.5, -1.0), line.Extend(2.5, -1.0));
            Assert.Equal(Lengthen2.ExtendToLength(line, 20.0, LineEnd.Start), line.ExtendToLength(20.0, LineEnd.Start));
        }

        [Theory]
        [InlineData(-10.0, 0.0)]
        [InlineData(0.0, -10.0)]
        [InlineData(-6.0, -4.0)]
        [InlineData(-9.99995, 0.0)]
        [InlineData(-20.0, 5.0)]
        public void Extend_ShorteningToNothingOrPastItself_IsRefused(double startDistance, double endDistance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.Extend(startDistance, endDistance));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Extend_ByANonFiniteDistance_IsRefused(double distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.Extend(distance, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.ExtendToLength(distance, LineEnd.End));
        }

        [Fact]
        public void Extend_ADegenerateSegment_HasNoDirectionToGoIn()
        {
            var point = new GeoLine2(3, 3, 3, 3);

            Assert.Throws<InvalidOperationException>(() => point.Extend(5.0, LineEnd.End));
            Assert.Throws<InvalidOperationException>(() => point.Extend(1.0, 1.0));
            Assert.Throws<InvalidOperationException>(() => point.ExtendToLength(5.0, LineEnd.End));
        }

        [Fact]
        public void Extend_AnUnknownEnd_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.Extend(1.0, (LineEnd)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.TryExtendTo(new GeoPoint2(20, 0), (LineEnd)7, out _));
        }

        [Fact]
        public void ExtendToLength_KeepsTheOtherEndAndDirection()
        {
            var line = new GeoLine2(0, 0, 3, 4);

            AssertLine(new GeoLine2(0, 0, 6, 8), line.ExtendToLength(10.0, LineEnd.End));
            AssertLine(new GeoLine2(-3, -4, 3, 4), line.ExtendToLength(10.0, LineEnd.Start));

            // Shorter than the segment: a trim.
            AssertLine(new GeoLine2(0, 0, 1.5, 2), line.ExtendToLength(2.5, LineEnd.End));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(0.00005)]
        public void ExtendToLength_OfNothing_IsRefused(double length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Horizontal.ExtendToLength(length, LineEnd.End));
        }

        #endregion

        #region To a point

        [Fact]
        public void ExtendToPoint_BringsTheEndLevelWithThePoint()
        {
            Assert.True(Horizontal.TryExtendTo(new GeoPoint2(15, 3), LineEnd.End, out GeoLine2 longer));
            AssertLine(new GeoLine2(0, 0, 15, 0), longer);

            Assert.True(Horizontal.TryExtendTo(new GeoPoint2(-4, 1), LineEnd.Start, out GeoLine2 earlier));
            AssertLine(new GeoLine2(-4, 0, 10, 0), earlier);
        }

        [Fact]
        public void ExtendToPoint_BehindTheEnd_IsATrimAndIsRefused()
        {
            Assert.False(Horizontal.TryExtendTo(new GeoPoint2(5, 3), LineEnd.End, out GeoLine2 unchanged));
            Assert.Equal(Horizontal, unchanged);

            // Behind the other end too: moving there would turn the segment round.
            Assert.False(Horizontal.TryExtendTo(new GeoPoint2(-4, 1), LineEnd.End, out _));
        }

        [Fact]
        public void TrimToPoint_ShortensWithinTheSegmentOnly()
        {
            Assert.True(Horizontal.TryTrimTo(new GeoPoint2(5, 3), LineEnd.End, out GeoLine2 trimmed));
            AssertLine(new GeoLine2(0, 0, 5, 0), trimmed);

            Assert.True(Horizontal.TryTrimTo(new GeoPoint2(5, 3), LineEnd.Start, out GeoLine2 trimmedStart));
            AssertLine(new GeoLine2(5, 0, 10, 0), trimmedStart);

            Assert.False(Horizontal.TryTrimTo(new GeoPoint2(15, 3), LineEnd.End, out _));

            // At the other end: nothing would be left.
            Assert.False(Horizontal.TryTrimTo(new GeoPoint2(0, 3), LineEnd.End, out _));
        }

        [Fact]
        public void AnEndAlreadyOnTheTarget_SatisfiesBothAndStays()
        {
            var target = new GeoPoint2(10, 7);

            Assert.True(Horizontal.TryExtendTo(target, LineEnd.End, out GeoLine2 extended));
            Assert.True(Horizontal.TryTrimTo(target, LineEnd.End, out GeoLine2 trimmed));
            AssertLine(Horizontal, extended);
            AssertLine(Horizontal, trimmed);

            // Within tolerance of the end counts as on it, and the end is snapped there.
            Assert.True(Horizontal.TryTrimTo(new GeoPoint2(10.00005, 7), LineEnd.End, out GeoLine2 snapped));
            Assert.Equal(10.00005, snapped.EndPoint.X, 12);
        }

        #endregion

        #region To a boundary segment

        [Fact]
        public void ExtendToSegment_StopsWhereTheLineMeetsIt()
        {
            Assert.True(Horizontal.TryExtendTo(new GeoLine2(20, -5, 20, 5), LineEnd.End, out GeoLine2 upright));
            AssertLine(new GeoLine2(0, 0, 20, 0), upright);

            // An oblique boundary crossing the line at x = 20.
            Assert.True(Horizontal.TryExtendTo(new GeoLine2(15, -5, 25, 5), LineEnd.End, out GeoLine2 oblique));
            AssertLine(new GeoLine2(0, 0, 20, 0), oblique);

            // Behind the start, reached by the start only.
            var behind = new GeoLine2(-5, -5, -5, 5);
            Assert.False(Horizontal.TryExtendTo(behind, LineEnd.End, out _));
            Assert.True(Horizontal.TryExtendTo(behind, LineEnd.Start, out GeoLine2 fromStart));
            AssertLine(new GeoLine2(-5, 0, 10, 0), fromStart);
        }

        [Fact]
        public void ExtendToSegment_CountsTheBoundaryOnlyWhereItIsDrawn()
        {
            // The boundary stops short of the line; AutoCAD's default EDGEMODE would not reach it either.
            Assert.False(Horizontal.TryExtendTo(new GeoLine2(20, 1, 20, 5), LineEnd.End, out _));

            // Its carrying line can still be reached by intersecting with the boundary extended.
            Assert.True(Horizontal.TryIntersectWith(new GeoLine2(20, 1, 20, 5), LineExtension.Both, out GeoPoint2 implied));
            Assert.True(Horizontal.TryExtendTo(implied, LineEnd.End, out GeoLine2 viaPoint));
            AssertLine(new GeoLine2(0, 0, 20, 0), viaPoint);
        }

        [Fact]
        public void ExtendToSegment_ParallelOrCollinear_IsNotACrossing()
        {
            Assert.False(Horizontal.TryExtendTo(new GeoLine2(0, 5, 10, 5), LineEnd.End, out _));
            Assert.False(Horizontal.TryExtendTo(new GeoLine2(15, 0, 25, 0), LineEnd.End, out _));
        }

        [Fact]
        public void TrimToSegment_CutsTheNamedEndBack()
        {
            var cutter = new GeoLine2(4, -1, 4, 1);

            Assert.True(Horizontal.TryTrimTo(cutter, LineEnd.End, out GeoLine2 keepStart));
            AssertLine(new GeoLine2(0, 0, 4, 0), keepStart);

            Assert.True(Horizontal.TryTrimTo(cutter, LineEnd.Start, out GeoLine2 keepEnd));
            AssertLine(new GeoLine2(4, 0, 10, 0), keepEnd);

            Assert.False(Horizontal.TryTrimTo(new GeoLine2(20, -1, 20, 1), LineEnd.End, out _));
        }

        #endregion

        #region To other boundaries

        private static readonly GeoPolygon2 Square = new GeoPolygon2(
            new GeoPoint2(20, -5), new GeoPoint2(30, -5), new GeoPoint2(30, 5), new GeoPoint2(20, 5));

        [Fact]
        public void ExtendToPolygon_StopsAtTheNearestEdgeBeyondTheEnd()
        {
            // From outside: where the line enters.
            Assert.True(Horizontal.TryExtendTo(Square, LineEnd.End, out GeoLine2 entering));
            AssertLine(new GeoLine2(0, 0, 20, 0), entering);

            // From inside: where it leaves.
            var inside = new GeoLine2(0, 0, 25, 0);
            Assert.True(inside.TryExtendTo(Square, LineEnd.End, out GeoLine2 leaving));
            AssertLine(new GeoLine2(0, 0, 30, 0), leaving);

            // Pointing away from it.
            Assert.False(new GeoLine2(10, 0, 0, 0).TryExtendTo(Square, LineEnd.End, out _));
        }

        [Fact]
        public void TrimToPolygon_StopsAtTheNearestEdgeWithinTheSegment()
        {
            Assert.True(new GeoLine2(0, 0, 25, 0).TryTrimTo(Square, LineEnd.End, out GeoLine2 cutAtEntry));
            AssertLine(new GeoLine2(0, 0, 20, 0), cutAtEntry);

            Assert.True(new GeoLine2(0, 0, 35, 0).TryTrimTo(Square, LineEnd.End, out GeoLine2 cutAtExit));
            AssertLine(new GeoLine2(0, 0, 30, 0), cutAtExit);

            Assert.False(Horizontal.TryTrimTo(Square, LineEnd.End, out _));
        }

        [Fact]
        public void ExtendAndTrim_ToAPolyline_UseEveryEdge()
        {
            var zigzag = new GeoPolyline2(
                new GeoPoint2(15, -5), new GeoPoint2(17, 5), new GeoPoint2(19, -5), new GeoPoint2(21, 5));

            Assert.True(Horizontal.TryExtendTo(zigzag, LineEnd.End, out GeoLine2 first));
            AssertLine(new GeoLine2(0, 0, 16, 0), first);

            Assert.True(new GeoLine2(0, 0, 19.5, 0).TryTrimTo(zigzag, LineEnd.End, out GeoLine2 back));
            AssertLine(new GeoLine2(0, 0, 18, 0), back);
        }

        [Fact]
        public void ExtendAndTrim_ToACircle_UseTheCircumference()
        {
            var circle = new GeoCircle2(new GeoPoint2(20, 0), 5.0);

            Assert.True(Horizontal.TryExtendTo(circle, LineEnd.End, out GeoLine2 toNearSide));
            AssertLine(new GeoLine2(0, 0, 15, 0), toNearSide);

            Assert.True(new GeoLine2(0, 0, 20, 0).TryExtendTo(circle, LineEnd.End, out GeoLine2 toFarSide));
            AssertLine(new GeoLine2(0, 0, 25, 0), toFarSide);

            Assert.True(new GeoLine2(0, 0, 30, 0).TryTrimTo(circle, LineEnd.End, out GeoLine2 trimmed));
            AssertLine(new GeoLine2(0, 0, 25, 0), trimmed);

            // Tangent: touched once, at the foot of the perpendicular from the centre.
            Assert.True(Horizontal.TryExtendTo(new GeoCircle2(new GeoPoint2(20, 5), 5.0), LineEnd.End, out GeoLine2 tangent));
            AssertLine(new GeoLine2(0, 0, 20, 0), tangent);

            // Missed altogether.
            Assert.False(Horizontal.TryExtendTo(new GeoCircle2(new GeoPoint2(20, 10), 5.0), LineEnd.End, out _));
        }

        [Fact]
        public void ExtendToRectangle_UsesItsFourEdges()
        {
            var rect = new GeoRectangle2(new GeoPoint2(25, 0), 10, 10);

            Assert.True(Horizontal.TryExtendTo(rect, LineEnd.End, out GeoLine2 extended));
            AssertLine(new GeoLine2(0, 0, 20, 0), extended);

            Assert.True(new GeoLine2(0, 0, 35, 0).TryTrimTo(rect, LineEnd.End, out GeoLine2 trimmed));
            AssertLine(new GeoLine2(0, 0, 30, 0), trimmed);
        }

        [Fact]
        public void ExtendThenExtendAgain_DoesNotMoveTheEndTwice()
        {
            Assert.True(Horizontal.TryExtendTo(Square, LineEnd.End, out GeoLine2 once));
            Assert.True(once.TryExtendTo(Square, LineEnd.End, out GeoLine2 twice));
            AssertLine(once, twice);
        }

        [Fact]
        public void ExtendOrTrim_FitsAnEndToABoundaryFromEitherSide()
        {
            var boundary = new GeoLine2(20, -5, 20, 5);

            foreach (var line in new[] { new GeoLine2(0, 0, 12, 0), new GeoLine2(0, 0, 27, 0) })
            {
                Assert.True(line.TryExtendTo(boundary, LineEnd.End, out GeoLine2 fitted) || line.TryTrimTo(boundary, LineEnd.End, out fitted));
                AssertLine(new GeoLine2(0, 0, 20, 0), fitted);
            }
        }

        [Fact]
        public void EveryTargetKind_AgreesBetweenCoreAndInstance()
        {
            var line = new GeoLine2(0, 0, 10, 1);
            var boundary = new GeoLine2(20, -5, 20, 5);
            var polyline = new GeoPolyline2(new GeoPoint2(20, -5), new GeoPoint2(20, 5));
            var circle = new GeoCircle2(new GeoPoint2(20, 0), 3);
            var rect = new GeoRectangle2(new GeoPoint2(25, 0), 10, 10);
            var point = new GeoPoint2(20, 3);

            Assert.Equal(Lengthen2.TryExtendTo(line, point, LineEnd.End, out GeoLine2 a1), line.TryExtendTo(point, LineEnd.End, out GeoLine2 b1));
            Assert.Equal(a1, b1);
            Assert.Equal(Lengthen2.TryExtendTo(line, boundary, LineEnd.End, out GeoLine2 a2), line.TryExtendTo(boundary, LineEnd.End, out GeoLine2 b2));
            Assert.Equal(a2, b2);
            Assert.Equal(Lengthen2.TryExtendTo(line, polyline, LineEnd.End, out GeoLine2 a3), line.TryExtendTo(polyline, LineEnd.End, out GeoLine2 b3));
            Assert.Equal(a3, b3);
            Assert.Equal(Lengthen2.TryExtendTo(line, Square, LineEnd.End, out GeoLine2 a4), line.TryExtendTo(Square, LineEnd.End, out GeoLine2 b4));
            Assert.Equal(a4, b4);
            Assert.Equal(Lengthen2.TryExtendTo(line, circle, LineEnd.End, out GeoLine2 a5), line.TryExtendTo(circle, LineEnd.End, out GeoLine2 b5));
            Assert.Equal(a5, b5);
            Assert.Equal(Lengthen2.TryExtendTo(line, rect, LineEnd.End, out GeoLine2 a6), line.TryExtendTo(rect, LineEnd.End, out GeoLine2 b6));
            Assert.Equal(a6, b6);

            var longLine = line.Extend(30.0, LineEnd.End);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, point, LineEnd.End, out GeoLine2 c1), longLine.TryTrimTo(point, LineEnd.End, out GeoLine2 d1));
            Assert.Equal(c1, d1);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, boundary, LineEnd.End, out GeoLine2 c2), longLine.TryTrimTo(boundary, LineEnd.End, out GeoLine2 d2));
            Assert.Equal(c2, d2);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, polyline, LineEnd.End, out GeoLine2 c3), longLine.TryTrimTo(polyline, LineEnd.End, out GeoLine2 d3));
            Assert.Equal(c3, d3);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, Square, LineEnd.End, out GeoLine2 c4), longLine.TryTrimTo(Square, LineEnd.End, out GeoLine2 d4));
            Assert.Equal(c4, d4);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, circle, LineEnd.End, out GeoLine2 c5), longLine.TryTrimTo(circle, LineEnd.End, out GeoLine2 d5));
            Assert.Equal(c5, d5);
            Assert.Equal(Lengthen2.TryTrimTo(longLine, rect, LineEnd.End, out GeoLine2 c6), longLine.TryTrimTo(rect, LineEnd.End, out GeoLine2 d6));
            Assert.Equal(c6, d6);
        }

        [Fact]
        public void NullBoundaries_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Horizontal.TryExtendTo((GeoPolygon2)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Horizontal.TryExtendTo((GeoPolyline2)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Horizontal.TryTrimTo((GeoPolygon2)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Horizontal.TryTrimTo((GeoPolyline2)null, LineEnd.End, out _));
        }

        [Fact]
        public void ADegenerateSegment_ReachesNothing()
        {
            var point = new GeoLine2(3, 3, 3, 3);

            Assert.False(point.TryExtendTo(Square, LineEnd.End, out GeoLine2 unchanged));
            Assert.Equal(point, unchanged);
            Assert.False(point.TryTrimTo(new GeoPoint2(3, 3), LineEnd.End, out _));
        }

        #endregion

        #region Corner

        [Fact]
        public void Corner_ExtendsTwoSegmentsThatStopShort()
        {
            var first = new GeoLine2(0, 0, 8, 0);
            var second = new GeoLine2(10, 2, 10, 10);

            Assert.True(first.TryTrimExtendToCorner(second, out GeoLine2 a, out GeoLine2 b));
            AssertLine(new GeoLine2(0, 0, 10, 0), a);
            AssertLine(new GeoLine2(10, 0, 10, 10), b);
            Assert.Equal(a.EndPoint, b.StartPoint);
        }

        [Fact]
        public void Corner_TrimsTwoSegmentsThatCrossKeepingTheLongerParts()
        {
            var first = new GeoLine2(0, 0, 10, 0);
            var second = new GeoLine2(3, -2, 3, 8);

            Assert.True(first.TryTrimExtendToCorner(second, out GeoLine2 a, out GeoLine2 b));
            AssertLine(new GeoLine2(3, 0, 10, 0), a);
            AssertLine(new GeoLine2(3, 0, 3, 8), b);
        }

        [Fact]
        public void Corner_OfParallelOrDegenerateSegments_IsRefused()
        {
            Assert.False(Horizontal.TryTrimExtendToCorner(new GeoLine2(0, 5, 10, 5), out GeoLine2 a, out GeoLine2 b));
            Assert.Equal(Horizontal, a);
            Assert.Equal(new GeoLine2(0, 5, 10, 5), b);

            Assert.False(Horizontal.TryTrimExtendToCorner(new GeoLine2(4, 4, 4, 4), out _, out _));
        }

        [Fact]
        public void Corner_IsTheSameWhicheverSegmentAsks()
        {
            var first = new GeoLine2(0, 0, 8, 1);
            var second = new GeoLine2(12, 3, 11, 10);

            Assert.True(Lengthen2.TryTrimExtendToCorner(first, second, out GeoLine2 a1, out GeoLine2 b1));
            Assert.True(Lengthen2.TryTrimExtendToCorner(second, first, out GeoLine2 b2, out GeoLine2 a2));
            AssertLine(a1, a2);
            AssertLine(b1, b2);
        }

        #endregion

        #region Invariance

        [Fact]
        public void ARigidMotionOfTheWholeScene_MovesEveryAnswerWithIt()
        {
            var rng = new Random(20260919);
            int checkedCases = 0;

            for (int trial = 0; trial < 300; trial++)
            {
                double angle = rng.NextDouble() * 2.0 * Math.PI;
                var shift = new GeoVector2(rng.NextDouble() * 2000 - 1000, rng.NextDouble() * 2000 - 1000);
                GeoPoint2 Move(GeoPoint2 p) => p.RotateBy(angle, new GeoPoint2(0, 0)).Add(shift);
                GeoLine2 MoveLine(GeoLine2 l) => new GeoLine2(Move(l.StartPoint), Move(l.EndPoint));

                var line = new GeoLine2(rng.NextDouble() * 10, rng.NextDouble() * 10, 20 + rng.NextDouble() * 10, rng.NextDouble() * 10);
                var polygon = new GeoPolygon2(
                    new GeoPoint2(35, -8), new GeoPoint2(50, -6), new GeoPoint2(48, 14), new GeoPoint2(36, 12));
                var circle = new GeoCircle2(new GeoPoint2(40, 4), 6.0);
                LineEnd end = trial % 2 == 0 ? LineEnd.End : LineEnd.Start;
                if (end == LineEnd.Start) { line = line.Reverse(); }

                var movedPolygon = new GeoPolygon2(new[] { Move(polygon[0]), Move(polygon[1]), Move(polygon[2]), Move(polygon[3]) });
                var movedCircle = new GeoCircle2(Move(circle.Center), circle.Radius);

                bool hitPolygon = line.TryExtendTo(polygon, end, out GeoLine2 toPolygon);
                Assert.Equal(hitPolygon, MoveLine(line).TryExtendTo(movedPolygon, end, out GeoLine2 movedToPolygon));
                if (hitPolygon) { AssertLine(MoveLine(toPolygon), movedToPolygon, 1e-7); checkedCases++; }

                bool hitCircle = line.TryExtendTo(circle, end, out GeoLine2 toCircle);
                Assert.Equal(hitCircle, MoveLine(line).TryExtendTo(movedCircle, end, out GeoLine2 movedToCircle));
                if (hitCircle) { AssertLine(MoveLine(toCircle), movedToCircle, 1e-7); }

                var other = new GeoLine2(60, -20 + rng.NextDouble() * 5, 55 + rng.NextDouble() * 10, 30);
                bool corner = line.TryTrimExtendToCorner(other, out GeoLine2 c1, out GeoLine2 c2);
                Assert.Equal(corner, MoveLine(line).TryTrimExtendToCorner(MoveLine(other), out GeoLine2 m1, out GeoLine2 m2));
                if (corner)
                {
                    AssertLine(MoveLine(c1), m1, 1e-7);
                    AssertLine(MoveLine(c2), m2, 1e-7);
                }
            }

            Assert.True(checkedCases > 100, $"only {checkedCases} extensions reached the polygon");
        }

        [Fact]
        public void EveryResult_StaysOnTheLineCarryingTheSegment()
        {
            var rng = new Random(77);

            for (int trial = 0; trial < 200; trial++)
            {
                var line = new GeoLine2(rng.NextDouble() * 10, rng.NextDouble() * 10, 15 + rng.NextDouble() * 10, 5 + rng.NextDouble() * 10);
                var boundary = new GeoLine2(40 + rng.NextDouble() * 10, -100, 40 + rng.NextDouble() * 10, 100);

                if (!line.TryExtendTo(boundary, LineEnd.End, out GeoLine2 result))
                {
                    continue;
                }

                // The start never moved, the direction is kept, and the new end lies on the boundary.
                Assert.Equal(line.StartPoint, result.StartPoint);
                Assert.True(Math.Abs(result.Direction.CrossProduct(line.Direction)) / (result.Length * line.Length) < 1e-12);
                Assert.True(result.Direction.DotProduct(line.Direction) > 0);
                Assert.True(boundary.DistanceTo(result.EndPoint) < 1e-9);
            }
        }

        #endregion
    }
}
