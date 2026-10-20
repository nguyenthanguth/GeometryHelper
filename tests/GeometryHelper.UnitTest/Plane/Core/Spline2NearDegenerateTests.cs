using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The places a spline meets another shape that a random draw never lands on, with the numbers worked out by hand: a tangent touch,
    /// a crossing at an end of the spline or of the shape, a partner through a cusp, a straight span lying along a segment, a spline of
    /// a circle lying along that circle, a touch within the point tolerance and not within a smaller one, and coordinates a million from
    /// the origin.
    /// </summary>
    public class Spline2NearDegenerateTests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-4, 1E-5);
        private static readonly Tolerance Coarse = new Tolerance(1E-2, 1E-5);

        private static GeoSpline2 Ring(double x = 0, double y = 0, double r = 50) => GeoSpline2.FromCircle(new GeoCircle2(P(x, y), r));

        private static GeoSpline2 Arch() => new GeoSpline2(3, new[] { P(0, 0), P(100, 150), P(200, 150), P(300, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

        /// <summary>
        /// Two curves that touch run side by side for a little way: where their gap is no more than the rounding of the numbers, a
        /// ten-thousandth of a millimetre either side of the touch for curvatures a twentieth apart, every point is as near a touch as any,
        /// so the place given is read to the point tolerance.
        /// </summary>
        private static void AssertTouch(GeoPoint2[] actual, double x, double y)
        {
            Assert.True(actual.Length == 1, actual.Length + " points " + string.Join(" ", actual) + ", one expected");
            Assert.True(actual[0].DistanceTo(P(x, y)) <= 1E-3, "the touch is at " + actual[0] + ", (" + x + ", " + y + ") expected");
        }

        private static void AssertPoints(GeoPoint2[] actual, params double[] xy)
        {
            Assert.True(actual.Length == xy.Length / 2, actual.Length + " points " + string.Join(" ", actual) + ", " + xy.Length / 2 + " expected");

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.True(actual[i].DistanceTo(P(xy[2 * i], xy[2 * i + 1])) <= 1E-6, "point " + i + " is " + actual[i] + ", (" + xy[2 * i] + ", " + xy[2 * i + 1] + ") expected");
            }
        }

        #region Touching

        [Fact]
        public void ALineTangentToTheTopOfTheArch_TouchesAtThePointAndIsNoughtFromIt()
        {
            GeoSpline2 arch = Arch();
            var line = new GeoXLine2(P(-50, 112.5), U(1, 0));

            Assert.Equal(0.0, arch.DistanceTo(line, Strict), 9);
            AssertPoints(arch.GetIntersections(line, Strict), 150, 112.5);
            Assert.True(arch.CollidesWith(line, Strict));
        }

        [Fact]
        public void ACircleInsideTheArchTouchingItsTop_TouchesAtTheTop()
        {
            // The circle of radius 20 about (150, 92.5): its top, (150, 112.5), is the top of the arch, whose curvature is a fifth of its own.
            GeoSpline2 arch = Arch();
            GeoSpline2 circle = Ring(150, 92.5, 20);

            Assert.Equal(0.0, arch.DistanceTo(circle, Strict), 9);
            AssertTouch(arch.GetIntersections(circle, Strict), 150, 112.5);
            AssertTouch(circle.GetIntersections(arch, Strict), 150, 112.5);
            Assert.Equal(arch.DistanceTo(circle, Strict), circle.DistanceTo(arch, Strict));
        }

        [Fact]
        public void TwoCirclesTouchingFromOutside_MeetAtOnePointFromEitherSide()
        {
            GeoSpline2 left = Ring(0, 0, 50);
            GeoSpline2 right = Ring(100, 0, 50);

            Assert.Equal(0.0, left.DistanceTo(right, Strict), 9);
            AssertPoints(left.GetIntersections(right, Strict), 50, 0);
            AssertPoints(right.GetIntersections(left, Strict), 50, 0);
        }

        [Fact]
        public void ASplineAndACircleOfTheSameCircle_LieAlongEachOther_NoughtApartAndCrossingNowhere()
        {
            GeoSpline2 ring = Ring();
            var circle = new GeoCircle2(P(0, 0), 50);

            Assert.Equal(0.0, ring.DistanceTo(circle, Strict), 9);
            Assert.True(ring.CollidesWith(circle, Strict));
            Assert.Empty(ring.GetIntersections(circle, Strict));
            Assert.Equal(0.0, ring.GetShortestLineTo(circle, Strict).Length, 9);
        }

        [Fact]
        public void ASplineOfAnArc_LiesAlongThatArcAndCrossesItNowhere_AndMeetsAnotherArcWhereTheCirclesDo()
        {
            var arc = new GeoArc2(P(0, 0), 50, Math.PI / 6, Math.PI);
            GeoSpline2 along = GeoSpline2.FromArc(arc);
            var other = new GeoArc2(P(60, 0), 80, Math.PI / 2, 3 * Math.PI / 2);

            Assert.Equal(0.0, along.DistanceTo(arc, Strict), 9);
            Assert.Empty(along.GetIntersections(arc, Strict));

            // The arc of radius 50 from 30 to 210 degrees and the arc of radius 80 about (60, 0) meet at (-2.5, 49.94) only: the other
            // crossing of the two circles, (-2.5, -49.94), is 266 degrees round, past the end of the first.
            AssertPoints(along.GetIntersections(other, Strict), -2.5, Math.Sqrt(2500 - 6.25));
        }

        #endregion

        #region Ends

        [Fact]
        public void ACornerOfAPolygonOnTheSpline_IsOnePlaceThoughTwoEdgesMeetThere()
        {
            // The square from (50, 0): its left side x = 50 is tangent to the ring at (50, 0), where its bottom edge crosses the ring.
            GeoSpline2 ring = Ring();
            var polygon = new GeoPolygon2(P(50, 0), P(100, 0), P(100, 100), P(50, 100));

            AssertPoints(ring.GetIntersections(polygon, Strict), 50, 0);
            Assert.Equal(0.0, ring.DistanceTo(polygon, Strict), 9);
        }

        [Fact]
        public void TheOriginOfARayOnTheSpline_IsAMeeting_WhichWayItPoints()
        {
            GeoSpline2 arch = Arch();
            var up = new GeoRay2(P(150, 112.5), U(0, 1));
            var down = new GeoRay2(P(150, 112.5), U(0, -1));

            AssertPoints(arch.GetIntersections(up, Strict), 150, 112.5);
            AssertPoints(arch.GetIntersections(down, Strict), 150, 112.5);
            Assert.Equal(0.0, arch.DistanceTo(up, Strict), 9);
        }

        [Fact]
        public void TheEndOfTheSplineOnASegment_IsAMeetingAtThatEnd()
        {
            GeoSpline2 arch = Arch();
            var segment = new GeoLine2(P(300, -50), P(300, 50));

            AssertPoints(arch.GetIntersections(segment, Strict), 300, 0);
            Assert.Equal(0.0, segment.DistanceTo(arch, Strict), 9);
        }

        [Fact]
        public void TheEndOfOneSplineOnTheMiddleOfAnother_IsAMeetingAtThatEnd()
        {
            GeoSpline2 arch = Arch();
            GeoSpline2 post = GeoSpline2.FromPolyline(new GeoPolyline2(P(300, 0), P(300, 80)));

            AssertPoints(arch.GetIntersections(post, Strict), 300, 0);
            AssertPoints(post.GetIntersections(arch, Strict), 300, 0);
        }

        [Fact]
        public void ALineThroughTheStartOfAClosedSpline_IsOneMeetingNotTwo()
        {
            // The ring starts and ends at (50, 0), and the line along X through the origin meets it there and at (-50, 0).
            GeoSpline2 ring = Ring();
            var line = new GeoXLine2(P(0, 0), U(1, 0));

            AssertPoints(ring.GetIntersections(line, Strict), 50, 0, -50, 0);
        }

        #endregion

        #region A cusp

        /// <summary>
        /// The curve y squared = x cubed over 100, x = 100 t squared, y = 100 t cubed, for t from -1 to 1, as one cubic spline: two Bézier
        /// pieces that meet in a cusp at the origin, where the speed is nought and both branches run along the X axis.
        /// </summary>
        private static GeoSpline2 Cusp()
        {
            return new GeoSpline2(
                3,
                new[] { P(100, -100), P(100.0 / 3.0, 0), P(0, 0), P(0, 0), P(0, 0), P(100.0 / 3.0, 0), P(100, 100) },
                new double[] { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2 });
        }

        [Fact]
        public void ALineThroughTheCuspAcrossIt_MeetsTheCurveAtTheCuspOnly()
        {
            GeoSpline2 cusp = Cusp();
            var vertical = new GeoXLine2(P(0, -300), U(0, 1));

            Assert.Equal(0.0, cusp.DistanceTo(vertical, Strict), 9);
            AssertPoints(cusp.GetIntersections(vertical, Strict), 0, 0);
        }

        [Fact]
        public void ALineAlongTheTangentAtTheCusp_CrossesTheCurveThere()
        {
            // y = 100 t cubed changes sign at t = 0: the branches are on either side of the X axis, so the axis crosses at the cusp, and
            // nowhere else.
            GeoSpline2 cusp = Cusp();
            var horizontal = new GeoXLine2(P(-500, 0), U(1, 0));

            AssertPoints(cusp.GetIntersections(horizontal, Strict), 0, 0);
        }

        [Fact]
        public void ALineThroughTheCuspAndTheEnd_MeetsTheCurveAtBoth()
        {
            // y = x meets 100 t cubed = 100 t squared at t = 0 and t = 1: the cusp and the end (100, 100); the branch below never reaches it.
            GeoSpline2 cusp = Cusp();
            var diagonal = new GeoXLine2(P(-50, -50), U(1, 1));

            AssertPoints(cusp.GetIntersections(diagonal, Strict), 0, 0, 100, 100);
        }

        [Fact]
        public void ACircleAboutThePointNearTheCusp_IsMeasuredFromTheCusp()
        {
            // The circle of radius 10 about (-30, 0) is 20 from the cusp, which is the nearest of the curve to it: both branches run away to the right.
            GeoSpline2 cusp = Cusp();
            var circle = new GeoCircle2(P(-30, 0), 10);

            Assert.Equal(20.0, cusp.DistanceTo(circle, Strict), 9);
            GeoLine2 line = cusp.GetShortestLineTo(circle, Strict);
            Assert.True(line.StartPoint.DistanceTo(P(0, 0)) <= 1E-9);
            Assert.True(line.EndPoint.DistanceTo(P(-20, 0)) <= 1E-9);
        }

        #endregion

        #region Lying along a segment

        [Fact]
        public void AStraightSpanAlongASegment_IsNoughtFromItAndCrossesNowhereAlongTheOverlap()
        {
            // The polyline (0, 0), (100, 0), (100, 50) as a spline of degree one: its first span lies on the segment from (20, 0) to (60, 0).
            GeoSpline2 corner = GeoSpline2.FromPolyline(new GeoPolyline2(P(0, 0), P(100, 0), P(100, 50)));
            var inside = new GeoLine2(P(20, 0), P(60, 0));
            var past = new GeoLine2(P(50, 0), P(150, 0));

            Assert.Equal(0.0, corner.DistanceTo(inside, Strict), 9);
            Assert.True(corner.CollidesWith(inside, Strict));
            Assert.Empty(corner.GetIntersections(inside, Strict));

            // The corner at (100, 0) leaves the segment past it there, which is a meeting; the overlap before it is not.
            AssertPoints(corner.GetIntersections(past, Strict), 100, 0);
            Assert.Equal(0.0, past.DistanceTo(corner, Strict), 9);
        }

        [Fact]
        public void ASplineAndItsOwnCopy_LieAlongEachOther()
        {
            GeoSpline2 arch = Arch();
            GeoSpline2 copy = Arch();

            Assert.Equal(0.0, arch.DistanceTo(copy, Strict));
            Assert.True(arch.CollidesWith(copy, Strict));
            Assert.Empty(arch.GetIntersections(copy, Strict));
            Assert.Equal(0.0, arch.GetShortestLineTo(copy, Strict).Length);
        }

        [Fact]
        public void ASplineCutInTwoAtAKnot_LiesAlongTheWhole_AndTheJoinIsNotACrossing()
        {
            // The two halves of the arch are the arch: each lies along it for its length, so the distance is nought and nothing crosses,
            // the cut itself, where one half ends and the other begins, included.
            GeoSpline2 arch = Arch();
            GeoSpline2[] halves = arch.Split(0.5);

            Assert.Equal(0.0, arch.DistanceTo(halves[0], Strict), 9);
            Assert.Equal(0.0, halves[1].DistanceTo(arch, Strict), 9);
            Assert.Empty(halves[0].GetIntersections(arch, Strict));
        }

        #endregion

        #region The point tolerance

        [Fact]
        public void ATouchHalfAThousandthOff_IsATouchAtTheDefaultToleranceAndNotAtAFinerOne()
        {
            GeoSpline2 ring = Ring();
            var line = new GeoXLine2(P(-100, 50.0005), U(1, 0));

            // The distance is what it is whatever the tolerance; it is the collision and the places that read it.
            Assert.Equal(0.0005, ring.DistanceTo(line, Fine), 9);
            Assert.Equal(0.0005, ring.DistanceTo(line, Coarse), 9);
            Assert.False(ring.CollidesWith(line, Fine));
            Assert.True(ring.CollidesWith(line, Strict));
            Assert.Empty(ring.GetIntersections(line, Fine));
            AssertPoints(ring.GetIntersections(line, Strict), 0, 50);
            AssertPoints(ring.GetIntersections(line, Coarse), 0, 50);
        }

        [Fact]
        public void ATouchTwoThousandthsOff_IsATouchAtACoarseToleranceOnly()
        {
            GeoSpline2 ring = Ring();
            var line = new GeoXLine2(P(-100, 50.002), U(1, 0));

            Assert.False(ring.CollidesWith(line, Strict));
            Assert.Empty(ring.GetIntersections(line, Strict));
            Assert.True(ring.CollidesWith(line, Coarse));
            AssertPoints(ring.GetIntersections(line, Coarse), 0, 50);
        }

        [Fact]
        public void TwoCrossingsCloserThanTheToleranceAreOnePlace_AndFurtherApartAreTwo()
        {
            // The line a ten-thousand-millionth under the top of the ring, y = 50 - 1E-9, cuts it at x = +-sqrt(2 * 50 * 1E-9) = +-3.16E-4:
            // two crossings 6.3E-4 apart, which are one place at the default tolerance of a thousandth and two at a ten-thousandth.
            GeoSpline2 ring = Ring();
            double high = 50 - 1E-9;
            var line = new GeoXLine2(P(-100, high), U(1, 0));
            double half = Math.Sqrt(2500 - high * high);

            Assert.InRange(half, 3.1E-4, 3.2E-4);
            Assert.Equal(2, ring.GetIntersections(line, Fine).Length);
            Assert.Single(ring.GetIntersections(line, Strict));
            Assert.Single(ring.GetIntersections(line, Coarse));
        }

        #endregion

        #region Far from the origin

        [Fact]
        public void TheRingAMillionFromTheOrigin_AnswersAsAtTheOrigin()
        {
            GeoSpline2 ring = Ring(1E6, -3E6);
            double shift = 1E6;
            double down = -3E6;
            var line = new GeoLine2(P(shift - 100, down + 30), P(shift + 100, down + 30));
            var polygon = new GeoPolygon2(P(shift + 50, down), P(shift + 100, down), P(shift + 100, down + 100), P(shift + 50, down + 100));
            var xline = new GeoXLine2(P(shift - 100, down + 80), U(1, 0));

            double at30 = Math.Sqrt(2500 - 900);
            AssertPoints(ring.GetIntersections(line, Strict).Select(p => P(p.X - shift, p.Y - down)).ToArray(), at30, 30, -at30, 30);
            Assert.Equal(30.0, ring.DistanceTo(xline, Strict), 6);
            Assert.Equal(0.0, ring.DistanceTo(polygon, Strict), 6);
            Assert.Equal(ring.DistanceTo(xline, Strict), xline.DistanceTo(ring, Strict));
        }

        #endregion

        #region A loop of the spline through the same point twice

        [Fact]
        public void ASplineCrossingItself_AtAPointALineGoesThrough_GivesThePointOnce()
        {
            // The cubic (-100, 0), (500, 300), (-500, 300), (100, 0) has a loop: x is nought at t = 0.067, 0.5 and 0.933, and y = 900 t (1 - t)
            // is 56.25 at the first and the last, so the curve crosses itself at (0, 56.25). The line along X at that height meets it
            // nowhere else, so it meets it at that one point, twice over, which is one place.
            GeoSpline2 loop = GeoSpline2.FromControlPoints(3, new[] { P(-100, 0), P(500, 300), P(-500, 300), P(100, 0) });
            var line = new GeoXLine2(P(-300, 56.25), U(1, 0));
            GeoPoint2[] crossings = loop.GetIntersections(line, Strict);

            Assert.Single(crossings);
            Assert.True(crossings[0].DistanceTo(P(0, 56.25)) <= 1E-6, "the crossing is " + crossings[0]);
            Assert.Equal(0.0, loop.DistanceTo(line, Strict), 9);
        }

        #endregion
    }
}
