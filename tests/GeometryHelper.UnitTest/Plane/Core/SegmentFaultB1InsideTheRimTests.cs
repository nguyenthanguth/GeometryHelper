using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment whose line runs inside a circle's rim within the point tolerance of it, but which crosses the rim nowhere
    /// along its length, still touches the circle where it comes within the tolerance.
    /// </summary>
    /// <remarks>
    /// Keeping the two true crossings of such a line, each where it lies, leaves none on a short segment between them,
    /// though the segment lies within the tolerance of the rim. It touches once: at the foot of the perpendicular from the
    /// centre when the foot is on it, the answer the circle always gave, else at its end nearer the rim.
    /// </remarks>
    public class SegmentFaultB1InsideTheRimTests
    {
        [Fact]
        public void ASegmentThreeHundredthsInsideTheRimAcrossTheFoot_TouchesAtTheFoot()
        {
            // The line y = 49.97 crosses the circle of radius 50 at x = -1.73 and 1.73, both off the segment from
            // (-0.2, 49.97) to (0.2, 49.97); the segment lies 0.03 inside the rim, within the tolerance of 0.1.
            var segment = new GeoLine2(P(-0.2, 49.97), P(0.2, 49.97));

            GeoPoint2 touch = Assert.Single(segment.GetIntersections(Circle(), Tenth));
            Assert.Equal(0.0, touch.DistanceTo(P(0, 49.97)), 12);
            Assert.True(Intersection2.TryIntersectWith(Circle(), segment, out GeoPoint2[] fromTheCircle, Tenth));
            Assert.Equal(0.0, Assert.Single(fromTheCircle).DistanceTo(P(0, 49.97)), 12);
        }

        [Fact]
        public void ASegmentThreeHundredthsInsideTheRimBesideTheFoot_TouchesAtItsEndNearerTheRim()
        {
            // From (0.3, 49.97) to (0.5, 49.97): the foot (0, 49.97) is 0.3 behind its start, and its end is the nearer
            // the rim, 0.0275 inside it.
            var segment = new GeoLine2(P(0.3, 49.97), P(0.5, 49.97));

            GeoPoint2 touch = Assert.Single(segment.GetIntersections(Circle(), Tenth));
            Assert.Equal(0.0, touch.DistanceTo(P(0.5, 49.97)), 12);
        }

        [Fact]
        public void ASegmentEndingTwoTenThousandthsFromTheEndOfAnArc_TouchesItAtTheFoot()
        {
            // The arc from 90 to 100 degrees on the circle of radius 50 starts at (0, 50). The segment from (0.5, 49.9998)
            // to (0, 49.9998) runs 0.0002 inside the rim and crosses it at x = 0.1414, short of the arc; its end, the foot
            // of the perpendicular from the centre, lies 0.0002 from the arc's start.
            var arc = new GeoArc2(P(0, 0), 50, Math.PI / 2, Math.PI / 2 + Math.PI / 18);
            var segment = new GeoLine2(P(0.5, 49.9998), P(0, 49.9998));

            GeoPoint2 touch = Assert.Single(segment.GetIntersections(arc, Thousandth));
            Assert.Equal(0.0, touch.DistanceTo(P(0, 49.9998)), 12);
            Assert.True(segment.CollidesWith(arc, Thousandth));
            Assert.Equal(0.0, segment.GetShortestLineTo(arc, Thousandth).Length, 12);
        }

        [Fact]
        public void ASegmentEndingSevenTenThousandthsFromTheEndOfAnArcAwayFromTheFoot_TouchesItAtThatEnd()
        {
            // The arc from 89.9 degrees starts at (0.0873, 49.99992); the segment from (0.5, 49.9998) to (0.088, 49.9998)
            // crosses the rim short of it, has the foot (0, 49.9998) off it, and ends 0.0007 from the arc's start.
            double start = (89.9 / 180) * Math.PI;
            var arc = new GeoArc2(P(0, 0), 50, start, start + Math.PI / 18);
            var segment = new GeoLine2(P(0.5, 49.9998), P(0.088, 49.9998));

            GeoPoint2 touch = Assert.Single(segment.GetIntersections(arc, Thousandth));
            Assert.Equal(0.0, touch.DistanceTo(P(0.088, 49.9998)), 9);
        }

        [Fact]
        public void ExtendingAnEndThreeHundredthsInsideTheRim_LeavesItWhereItIs()
        {
            // The line y = 49.97 runs 0.03 inside the rim of the circle of radius 50 and crosses it at x = 1.73, off the
            // segment from (-0.4, 49.97) to (-0.3, 49.97); the end (-0.3, 49.97) lies 0.029 inside the rim, within 0.1.
            var segment = new GeoLine2(P(-0.4, 49.97), P(-0.3, 49.97));

            Assert.True(segment.TryExtendTo(Circle(), LineEnd.End, out GeoLine2 extended, Tenth));
            Assert.Equal(0.0, extended.StartPoint.DistanceTo(P(-0.4, 49.97)), 12);
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(-0.3, 49.97)), 12);
        }

        [Fact]
        public void ExtendingTheMirroredEndThreeHundredthsInsideTheRim_LeavesItWhereItIs()
        {
            // The same segment turned over x = 0, from (0.4, 49.97) to (0.3, 49.97).
            var segment = new GeoLine2(P(0.4, 49.97), P(0.3, 49.97));

            Assert.True(segment.TryExtendTo(Circle(), LineEnd.End, out GeoLine2 extended, Tenth));
            Assert.Equal(0.0, extended.StartPoint.DistanceTo(P(0.4, 49.97)), 12);
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(0.3, 49.97)), 12);
        }
    }
}
