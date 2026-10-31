using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A side of a straight polygon, polyline or rectangle no longer than the point tolerance is the side it is to the
    /// closest edge and the shortest line, as it already was to the distance and as it is for the curved loops and chains,
    /// and since G1wh to the nearest point too;
    /// and the exact distance from a segment to an arc smaller than the tolerance weighs the point of the arc facing the
    /// segment however near the segment passes to the centre.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The polygon is (0, 0), (10, 0), (10, 0.006), (0, 10), the polyline the hairpin from (0, 0) to (10, 0), on to
    /// (10, 0.006) and back to (0, 0.006), and the rectangle 10 by 0.006 on the origin; each has a side 0.006 long at
    /// x = 10, and each is asked at a tolerance of a hundredth. The shapes asked about lie 0.03 to the right of the middle
    /// of that side, where its corners are 0.0301496 away.
    /// </para>
    /// <para>
    /// The closest edges of the straight shapes take no tolerance; they read the side shorter than the global one as its
    /// start, so they are asked of a rectangle 10 by 0.0006 and of the triangle (0, 0), (10, 0), (10, 0.0006), whose side
    /// 0.0006 long is shorter than the default of a thousandth. No test sets the global tolerance; every other one is
    /// passed.
    /// </para>
    /// </remarks>
    public class ShortEdgeG1wTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        /// <summary>How far each corner of the short side is from a point 0.03 to the right of its middle.</summary>
        private static readonly double ToACorner = Math.Sqrt(0.03 * 0.03 + 0.003 * 0.003);

        private static GeoPolygon2 Polygon()
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 10));

        private static GeoPolyline2 Hairpin()
            => new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 0.006));

        private static GeoRectangle2 Strip() => new GeoRectangle2(0, 0, 10, 0.006);

        private static GeoRectangle2 ThinStrip() => new GeoRectangle2(0, 0, 10, 0.0006);

        private static bool IsTheRightSide(GeoLine2 edge, double top)
            => Math.Abs(edge.StartPoint.X - 10) < 1E-12 && Math.Abs(edge.EndPoint.X - 10) < 1E-12
                && Math.Abs(Math.Abs(edge.EndPoint.Y - edge.StartPoint.Y) - top) < 1E-12;

        [Fact]
        public void TheShortestLineToASegmentBesideTheMiddleOfAShortStraightSide_IsAsLongAsTheDistance()
        {
            // It ran from a corner of the side, 0.0301496 long, where the distance is 0.03.
            var segment = new GeoLine2(new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, 0.003));
            GeoLine2 fromPolygon = Polygon().GetShortestLineTo(segment, Hundredth);

            Assert.Equal(0.03, Polygon().DistanceTo(segment), 12);
            Assert.Equal(0.03, fromPolygon.Length, 12);
            Assert.Equal(10, fromPolygon.StartPoint.X, 12);
            Assert.Equal(0.003, fromPolygon.StartPoint.Y, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(segment, Hundredth).Length, 12);
            Assert.Equal(0.03, Strip().GetShortestLineTo(segment, Hundredth).Length, 12);
            Assert.Equal(0.03, segment.GetShortestLineTo(Polygon(), Hundredth).Length, 12);
            Assert.Equal(0.03, segment.GetShortestLineTo(Hairpin(), Hundredth).Length, 12);
            Assert.Equal(0.03, segment.GetShortestLineTo(Strip(), Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToACircleBesideTheMiddleOfAShortStraightSide_IsAsLongAsTheGapToItsRim()
        {
            // The circle of radius 0.02 about (10.05, 0.003) is 0.03 from the side; from a corner it is 0.0500899 less 0.02.
            var circle = new GeoCircle2(new GeoPoint2(10.05, 0.003), 0.02);

            Assert.Equal(0.03, Polygon().GetShortestLineTo(circle, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(circle, Hundredth).Length, 12);
            Assert.Equal(0.03, Strip().GetShortestLineTo(circle, Hundredth).Length, 12);
            Assert.Equal(0.03, circle.GetShortestLineTo(Polygon(), Hundredth).Length, 12);
            Assert.Equal(0.03, circle.GetShortestLineTo(Strip(), Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToAShapePointingAtTheMiddleOfAShortStraightSide_IsAsLongAsTheDistance()
        {
            // A triangle, a polyline and a square turned 45 degrees, each with a corner at (10.03, 0.003) and running away
            // to the right, so no corner of the short side has a nearer side of theirs to project onto.
            var triangle = new GeoPolygon2(new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, -0.01), new GeoPoint2(10.05, 0.02));
            var polyline = new GeoPolyline2(new GeoPoint2(10.05, -0.01), new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, 0.02));
            var diamond = new GeoRectangle2(new GeoPoint2(10.03 + 0.01 * Math.Sqrt(2), 0.003), 0.02, 0.02, Math.PI / 4);

            Assert.True(ToACorner - 0.03 > 1E-4);
            Assert.Equal(0.03, Polygon().GetShortestLineTo(triangle, Hundredth).Length, 9);
            Assert.Equal(0.03, Polygon().GetShortestLineTo(polyline, Hundredth).Length, 9);
            Assert.Equal(0.03, Polygon().GetShortestLineTo(diamond, Hundredth).Length, 9);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(triangle, Hundredth).Length, 9);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(polyline, Hundredth).Length, 9);
            Assert.Equal(0.03, Strip().GetShortestLineTo(triangle, Hundredth).Length, 9);
            Assert.Equal(0.03, Strip().GetShortestLineTo(diamond, Hundredth).Length, 9);
            Assert.Equal(0.03, triangle.GetShortestLineTo(Strip(), Hundredth).Length, 9);
        }

        [Fact]
        public void APointBesideTheMiddleOfASideShorterThanTheGlobalTolerance_HasThatSideForItsClosestEdge()
        {
            // The side is 0.03 from (10.03, 0.0003); read as its start, it stood 0.0300015 off, as far as the bottom side,
            // which came first and was taken.
            var point = new GeoPoint2(10.03, 0.0003);
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.0006));

            Assert.True(IsTheRightSide(ThinStrip().GetClosestEdge(point), 0.0006));
            Assert.True(IsTheRightSide(triangle.GetClosestEdge(point), 0.0006));
        }

        [Fact]
        public void ASegmentAndACircleBesideTheMiddleOfASideShorterThanTheGlobalTolerance_HaveThatSideForTheirClosestEdge()
        {
            var segment = new GeoLine2(new GeoPoint2(10.03, 0.0003), new GeoPoint2(10.05, 0.0003));
            var circle = new GeoCircle2(new GeoPoint2(10.05, 0.0003), 0.02);
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.0006));

            Assert.True(IsTheRightSide(ThinStrip().GetClosestEdge(segment), 0.0006));
            Assert.True(IsTheRightSide(ThinStrip().GetClosestEdge(circle), 0.0006));
            Assert.True(IsTheRightSide(triangle.GetClosestEdge(segment), 0.0006));
            Assert.True(IsTheRightSide(triangle.GetClosestEdge(circle), 0.0006));
        }

        [Fact]
        public void TheNearestPointBesideAShortStraightSide_IsOnTheSideAsItIs()
        {
            // A side is measured as it is (G1wh), as for the curved loops since G1wg: the line from the polygon and from the
            // polyline to (10.03, 0.003) runs from the foot (10, 0.003), as long as the distance, 0.03, where it ran from
            // (10, 0), 0.0301496 long. A rectangle measures its sides exactly, and keeps doing so.
            var point = new GeoPoint2(10.03, 0.003);
            GeoLine2 fromPolygon = Polygon().GetShortestLineTo(point, Hundredth);

            Assert.True(ToACorner - 0.03 > 1E-4);
            Assert.Equal(10, fromPolygon.StartPoint.X, 12);
            Assert.Equal(0.003, fromPolygon.StartPoint.Y, 12);
            Assert.Equal(0.03, fromPolygon.Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(point, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().DistanceTo(point), 12);
            Assert.Equal(0.03, Strip().GetShortestLineTo(point, Hundredth).Length, 12);
        }

        [Fact]
        public void ASegmentPassingNearTheCentreOfAnArcSmallerThanTheTolerance_IsTheGapToTheArcsMiddleAway()
        {
            // The half circle of radius 0.004 about the origin, below it, and the segment along y = -0.008, 0.008 from the
            // centre: the bottom of the arc is 0.004 from the segment, where its ends, all that was weighed, are 0.008 off.
            var arc = new GeoArc2(new GeoPoint2(0, 0), 0.004, Math.PI, 2 * Math.PI);
            var segment = new GeoLine2(new GeoPoint2(-5, -0.008), new GeoPoint2(5, -0.008));

            Assert.Equal(0.004, arc.DistanceTo(segment, Hundredth), 12);
            Assert.Equal(0.004, segment.DistanceTo(arc, Hundredth), 12);
            Assert.True(arc.CollidesWith(segment, Hundredth));
        }

        [Fact]
        public void ASegmentPassingNearTheCentreOfAnArcSmallerThanTheTolerance_HasThatArcForItsClosestEdge()
        {
            // The chain runs straight from (-2, -0.002), 0.006 from the segment, to (-0.004, 0), then round the half circle
            // of radius 0.004 below the origin, 0.004 from it; the straight edge was taken, the arc measured 0.008 off.
            var chain = new GeoPolylineArc2(new[]
            {
                new GeoEdge2(new GeoPoint2(-2, -0.002), new GeoPoint2(-0.004, 0)),
                new GeoEdge2(new GeoPoint2(-0.004, 0), new GeoPoint2(0.004, 0), 1.0),
            });
            var segment = new GeoLine2(new GeoPoint2(-5, -0.008), new GeoPoint2(5, -0.008));

            Assert.Equal(0.004, chain.DistanceTo(segment, Hundredth), 12);
            Assert.True(chain.GetClosestEdge(segment, Hundredth).IsArc);
        }

        [Fact]
        public void TwoSegmentsWhoseLinesCrossJustPastTheEndsOfBoth_AreJoinedByTheGapBetweenThem()
        {
            // The segment from the origin to (10, 0), and the one leaving (10.00135, 0.00078) at 60 degrees, whose line
            // crosses the first's 0.0009 past the end of each: they are 0.00156 apart, more than the thousandth, and the
            // shortest line read them as touching.
            var thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);
            var first = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var start = new GeoPoint2(10.0009 + 0.0009 * Math.Cos(Math.PI / 3), 0.0009 * Math.Sin(Math.PI / 3));
            var second = new GeoLine2(start, new GeoPoint2(start.X + 5, start.Y + 5 * Math.Sqrt(3)));
            double gap = first.DistanceTo(second, thousandth);

            Assert.Equal(start.DistanceTo(new GeoPoint2(10, 0)), gap, 12);
            Assert.True(gap > 0.0015);
            Assert.False(first.CollidesWith(second, thousandth));
            Assert.Equal(gap, first.GetShortestLineTo(second, thousandth).Length, 12);
            Assert.Equal(gap, second.GetShortestLineTo(first, thousandth).Length, 12);
        }

        [Fact]
        public void TwoSquaresWhoseCornersAlmostMeet_AreJoinedByTheGapBetweenTheCorners()
        {
            // Two squares 100 by 100, the corner of the second 0.00085 off the first's in x and in y: 0.0012 apart, where
            // their sides cross 0.00085 past the corner of each and the shortest line read them as touching.
            var thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);
            var first = new GeoRectangle2(0, 0, 100, 100);
            var second = new GeoRectangle2(100.00085, 100.00085, 100, 100);
            double gap = 0.00085 * Math.Sqrt(2);

            Assert.Equal(gap, first.DistanceTo(second), 9);
            Assert.Equal(gap, first.GetShortestLineTo(second, thousandth).Length, 9);
            Assert.Equal(gap, first.ToPolygon().GetShortestLineTo(second.ToPolygon(), thousandth).Length, 9);
        }

        [Fact]
        public void TwoArcsWhoseCirclesCrossJustPastTheEndsOfBoth_AreJoinedByTheGapBetweenThem()
        {
            // The quarter circle of radius 50 from (50, 0) to (0, 50), and an arc of the same radius whose circle crosses
            // the first's 0.0009 on past that end, at 60 degrees, and which starts 0.0009 short of the crossing: their ends
            // are 0.00156 apart, more than the thousandth, and the shortest line read them as touching.
            var thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);
            var first = new GeoArc2(new GeoPoint2(0, 0), 50, 0, Math.PI / 2);
            double past = Math.PI / 2 + 0.0009 / 50;
            var crossing = new GeoPoint2(50 * Math.Cos(past), 50 * Math.Sin(past));
            var centre = new GeoPoint2(crossing.X - 50 * Math.Cos(Math.PI / 6), crossing.Y - 50 * Math.Sin(Math.PI / 6));
            double start = Math.PI / 6 + 0.0009 / 50;
            var second = new GeoArc2(centre, 50, start, start + 0.5);
            double gap = first.DistanceTo(second, thousandth);

            Assert.Equal(first.EndPoint.DistanceTo(second.StartPoint), gap, 9);
            Assert.True(gap > 0.0015);
            Assert.False(first.CollidesWith(second, thousandth));
            Assert.Equal(gap, first.GetShortestLineTo(second, thousandth).Length, 12);
            Assert.Equal(gap, second.GetShortestLineTo(first, thousandth).Length, 12);
        }
    }
}
