using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment meets a circle, or an arc, where it comes within the point tolerance of it, as the gap to the rim says:
    /// an end within the tolerance of the rim is a touch however far the segment's line runs on before it crosses, an end
    /// further off is none though the line passes within the tolerance just past it, and a segment that runs into the rim
    /// and stops within the tolerance of it touches once.
    /// </summary>
    /// <remarks>
    /// Every test passes a tolerance of a thousandth. The circle of radius 50 about the origin is asked of segments whose
    /// ends lie half a thousandth off its rim, the small circles of segments whose ends lie a little more or less than the
    /// tolerance off theirs.
    /// </remarks>
    public class CircleTouchG1wbTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        private static GeoCircle2 Circle50() => new GeoCircle2(new GeoPoint2(0, 0), 50);

        private static void AssertAt(GeoPoint2 expected, GeoPoint2 actual)
        {
            Assert.Equal(expected.X, actual.X, 12);
            Assert.Equal(expected.Y, actual.Y, 12);
        }

        [Fact]
        public void ASegmentLeavingTheRimWithinTheToleranceOutside_TouchesItAtItsStart()
        {
            // Its line runs 0.0005 above the top of the circle, and comes nearest it 0.05 behind the start, more than the
            // tolerance past the end; the start itself is 0.000525 off the rim.
            var start = new GeoPoint2(0.05, 50.0005);
            var segment = new GeoLine2(start, new GeoPoint2(10, 50.0005));
            GeoPoint2[] points = Circle50().GetIntersections(segment, Thousandth);

            Assert.Single(points);
            AssertAt(start, points[0]);
            Assert.True(segment.TryIntersectWith(Circle50(), out GeoPoint2[] again, Thousandth));
            Assert.Single(again);
            Assert.Single(Circle50().GetIntersections(new GeoRay2(start, new GeoVector2(1, 0)), Thousandth));
            Assert.Equal(0, segment.GetShortestLineTo(Circle50(), Thousandth).Length);
        }

        [Fact]
        public void ASegmentEndingWithinTheToleranceInsideTheRim_TouchesItAtThatEnd()
        {
            // Its line runs 0.05 inside the rim, and crosses it 0.0105 on past the end, more than the tolerance; the end is
            // 0.00046875 inside the rim.
            var end = new GeoPoint2(2.225, 49.95);
            var segment = new GeoLine2(new GeoPoint2(0, 49.95), end);
            GeoPoint2[] points = Circle50().GetIntersections(segment, Thousandth);

            Assert.Single(points);
            AssertAt(end, points[0]);
        }

        [Fact]
        public void APolylineWithACornerWithinTheToleranceOfTheRim_TouchesItThere()
        {
            var corner = new GeoPoint2(0.05, 50.0005);
            var polyline = new GeoPolyline2(corner, new GeoPoint2(10, 50.0005), new GeoPoint2(10, 60));
            GeoPoint2[] points = polyline.GetIntersections(Circle50(), Thousandth);

            Assert.Single(points);
            AssertAt(corner, points[0]);
        }

        [Fact]
        public void ASegmentEndingFurtherThanTheToleranceFromASmallCircle_DoesNotTouchIt()
        {
            // The circle of radius 0.001 about the origin; the segment's line runs 0.0009 above its top, its nearest point
            // 0.0009 behind the start, which is 0.0011024 off the rim. It read as a touch at the start.
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 0.001);
            var segment = new GeoLine2(new GeoPoint2(0.0009, 0.0019), new GeoPoint2(1, 0.0019));
            double gap = Math.Sqrt(0.0009 * 0.0009 + 0.0019 * 0.0019) - 0.001;
            var square = new GeoPolygon2(new GeoPoint2(0.0009, 0.0019), new GeoPoint2(1, 0.0019), new GeoPoint2(1, 1), new GeoPoint2(0.0009, 1));

            Assert.Empty(circle.GetIntersections(segment, Thousandth));
            Assert.Empty(circle.GetIntersections(new GeoRay2(segment.StartPoint, new GeoVector2(1, 0)), Thousandth));
            Assert.Equal(gap, segment.GetShortestLineTo(circle, Thousandth).Length, 12);
            Assert.Equal(gap, square.GetShortestLineTo(circle, Thousandth).Length, 12);
            Assert.Equal(gap, circle.GetShortestLineTo(square, Thousandth).Length, 12);
            Assert.Empty(square.GetIntersections(circle, Thousandth));
        }

        [Fact]
        public void ASegmentRunningIntoTheRimAndStoppingWithinTheToleranceOfIt_MeetsItOnce()
        {
            // The circle of radius 0.0012 about the origin: the segment crosses its rim at (-0.0012, 0) and stops 0.0008
            // short of it again, 0.0016 on; that end was a second meeting.
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 0.0012);
            GeoPoint2[] points = circle.GetIntersections(new GeoLine2(new GeoPoint2(-1, 0), new GeoPoint2(0.0004, 0)), Thousandth);

            Assert.Single(points);
            AssertAt(new GeoPoint2(-0.0012, 0), points[0]);
        }

        [Fact]
        public void ASegmentLyingWithinTheToleranceOfTheRimAlongItsLength_TouchesItOnceAtItsEndNearerTheRim()
        {
            // The circle of radius 0.0014 about the origin: the segment from (-0.0007, 0) to (0.0008, 0) stops 0.0007 and
            // 0.0006 short of the rim; both ends were meetings, 0.0015 apart.
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 0.0014);
            GeoPoint2[] points = circle.GetIntersections(new GeoLine2(new GeoPoint2(-0.0007, 0), new GeoPoint2(0.0008, 0)), Thousandth);

            Assert.Single(points);
            AssertAt(new GeoPoint2(0.0008, 0), points[0]);
        }

        [Fact]
        public void AnArcEndingWithinTheToleranceOfASegment_TouchesItBelowThatEnd()
        {
            // The arc of radius 50 about (0, 50.0002) from 0.0045 past its bottom round to its right: it starts at
            // (0.2249992, 0.0007062), within the tolerance above the segment along the x axis, while its circle passes
            // 0.0002 above the segment at x = 0, where the arc does not reach.
            var arc = new GeoArc2(new GeoPoint2(0, 50.0002), 50, -0.5 * Math.PI + 0.0045, 0);
            var segment = new GeoLine2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0));
            GeoPoint2[] points = arc.GetIntersections(segment, Thousandth);

            Assert.True(arc.CollidesWith(segment, Thousandth));
            Assert.Single(points);
            AssertAt(new GeoPoint2(arc.StartPoint.X, 0), points[0]);
            Assert.Single(segment.GetIntersections(arc, Thousandth));
        }
    }
}
