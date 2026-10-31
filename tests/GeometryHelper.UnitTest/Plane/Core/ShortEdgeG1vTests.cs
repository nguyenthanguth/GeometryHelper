using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight side of a curved loop or chain no longer than the point tolerance is the side it is to the closest edge and
    /// the shortest line, as it is to the distance: it was read as the point at its start, so a shape beside the middle of the
    /// side was measured to a corner of it instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every loop here is (0, 0), (10, 0), (10, 0.006), (0, 10), whose right-hand side is 0.006 long, and every chain the
    /// hairpin from (0, 0) to (10, 0), on to (10, 0.006) and back to (0, 0.006); both are asked at a tolerance of a
    /// hundredth, so that side is shorter than the tolerance and long enough for the global one of a thousandth to keep it.
    /// The shapes asked about lie 0.03 to the right of the middle of that side, three times the tolerance off, where the
    /// corners at (10, 0) and (10, 0.006) are 0.0301496 away.
    /// </para>
    /// <para>
    /// The nearest point reads the side as it is too (G1wg): a point beside a side no longer than the tolerance is no longer
    /// carried to the side's start, so the line to a point is as long as the distance there; it is still carried to an arc's
    /// start near its centre.
    /// </para>
    /// <para>
    /// No test sets the global tolerance; each passes its own.
    /// </para>
    /// </remarks>
    public class ShortEdgeG1vTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        /// <summary>How far each corner of the short side is from a point 0.03 to the right of its middle.</summary>
        private static readonly double ToACorner = Math.Sqrt(0.03 * 0.03 + 0.003 * 0.003);

        private static GeoPolygonArc2 Loop()
            => new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 10) });

        private static GeoPolylineArc2 Hairpin()
            => new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 0.006) });

        private static bool IsTheShortSide(GeoEdge2 edge)
            => edge.StartPoint.X == 10 && edge.StartPoint.Y == 0 && edge.EndPoint.X == 10 && edge.EndPoint.Y == 0.006;

        [Fact]
        public void APointBesideTheMiddleOfAShortSide_HasThatSideForItsClosestEdge()
        {
            // The side is 0.03 from (10.03, 0.003); read as its start, it stood 0.0301496 off, as far as the bottom side,
            // which came first and was taken.
            GeoPolygonArc2 loop = Loop();
            var point = new GeoPoint2(10.03, 0.003);

            Assert.Equal(4, loop.EdgeCount);
            Assert.True(IsTheShortSide(loop.GetClosestEdge(point, Hundredth)));
            Assert.True(IsTheShortSide(Hairpin().GetClosestEdge(point, Hundredth)));
        }

        [Fact]
        public void ASegmentBesideTheMiddleOfAShortSide_HasThatSideForItsClosestEdge()
        {
            var segment = new GeoLine2(new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, 0.003));

            Assert.True(IsTheShortSide(Loop().GetClosestEdge(segment, Hundredth)));
            Assert.True(IsTheShortSide(Hairpin().GetClosestEdge(segment, Hundredth)));
        }

        [Fact]
        public void ACircleAndAnArcBesideTheMiddleOfAShortSide_HaveThatSideForTheirClosestEdge()
        {
            var circle = new GeoCircle2(new GeoPoint2(10.05, 0.003), 0.02);
            GeoArc2 arc = GeoArc2.FromThreePoints(new GeoPoint2(10.035, -0.02), new GeoPoint2(10.03, 0.003), new GeoPoint2(10.035, 0.026));

            Assert.True(IsTheShortSide(Loop().GetClosestEdge(circle, Hundredth)));
            Assert.True(IsTheShortSide(Loop().GetClosestEdge(arc, Hundredth)));
            Assert.True(IsTheShortSide(Hairpin().GetClosestEdge(circle, Hundredth)));
            Assert.True(IsTheShortSide(Hairpin().GetClosestEdge(arc, Hundredth)));
        }

        [Fact]
        public void TheShortestLineToASegmentBesideTheMiddleOfAShortSide_IsAsLongAsTheDistance()
        {
            // It ran from a corner of the side, 0.0301496 long, where the distance is 0.03.
            var segment = new GeoLine2(new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, 0.003));
            GeoLine2 fromLoop = Loop().GetShortestLineTo(segment, Hundredth);
            GeoLine2 fromChain = Hairpin().GetShortestLineTo(segment, Hundredth);

            Assert.Equal(0.03, Loop().DistanceTo(segment, Hundredth), 12);
            Assert.Equal(0.03, fromLoop.Length, 12);
            Assert.Equal(0.03, fromChain.Length, 12);
            Assert.Equal(10, fromLoop.StartPoint.X, 12);
            Assert.Equal(0.003, fromLoop.StartPoint.Y, 12);
            Assert.Equal(0.03, segment.GetShortestLineTo(Loop(), Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToACircleBesideTheMiddleOfAShortSide_IsAsLongAsTheGapToItsRim()
        {
            // The circle of radius 0.02 about (10.05, 0.003) is 0.03 from the side; from a corner it is 0.0500899 less 0.02.
            var circle = new GeoCircle2(new GeoPoint2(10.05, 0.003), 0.02);

            Assert.Equal(0.03, Loop().GetShortestLineTo(circle, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(circle, Hundredth).Length, 12);
            Assert.Equal(0.03, circle.GetShortestLineTo(Loop(), Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToAnArcBowingTowardsTheMiddleOfAShortSide_IsAsLongAsTheDistance()
        {
            // The arc through (10.035, -0.02), (10.03, 0.003) and (10.035, 0.026) comes nearest the side at (10.03, 0.003).
            GeoArc2 arc = GeoArc2.FromThreePoints(new GeoPoint2(10.035, -0.02), new GeoPoint2(10.03, 0.003), new GeoPoint2(10.035, 0.026));
            double distance = Hairpin().DistanceTo(arc, Hundredth);

            Assert.Equal(0.03, distance, 9);
            Assert.True(ToACorner - distance > 1E-4);
            Assert.Equal(distance, Loop().GetShortestLineTo(arc, Hundredth).Length, 12);
            Assert.Equal(distance, Hairpin().GetShortestLineTo(arc, Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToAPolylineBesideTheMiddleOfAShortSide_IsAsLongAsTheDistance()
        {
            var polyline = new GeoPolyline2(new GeoPoint2(10.03, 0.003), new GeoPoint2(10.05, 0.003), new GeoPoint2(10.05, 0.05));

            Assert.Equal(0.03, Loop().GetShortestLineTo(polyline, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(polyline, Hundredth).Length, 12);
        }

        [Fact]
        public void TheShortestLineToASegmentBesideAShortSideOfALoopInSpace_IsAsLongAsTheDistance()
        {
            // The loop in space lays itself out in its plane and asks the loop there: the same loop in the plane z = 5, built
            // within a billionth, at a tolerance of a hundredth.
            var build = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);
            var loop = new GeoPolygonArc3(
                new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5), new GeoPoint3(10, 0.006, 5), new GeoPoint3(0, 10, 5) }, null, null, build);
            var segment = new GeoLine3(new GeoPoint3(10.03, 0.003, 5), new GeoPoint3(10.05, 0.003, 5));

            Assert.Equal(0.03, loop.GetShortestLineTo(segment, Hundredth).Length, 12);
        }

        [Fact]
        public void TheNearestPointBesideAShortSide_IsOnTheSideAsItIs()
        {
            // A side is measured as it is (G1wg), no longer snapped to its start: the nearest point of the loop to
            // (10.03, 0.003) is the foot (10, 0.003), 0.03 away, and the line to the point runs from there, as long as the
            // distance, where it ran from (10, 0), 0.0301496 long.
            GeoPolygonArc2 loop = Loop();
            var point = new GeoPoint2(10.03, 0.003);
            GeoPoint2 nearest = loop.GetClosestPointOnBoundary(point, Hundredth);

            Assert.Equal(10, nearest.X, 12);
            Assert.Equal(0.003, nearest.Y, 12);
            Assert.Equal(0.03, loop.GetShortestLineTo(point, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().DistanceTo(point, Hundredth), 12);
        }
    }
}
