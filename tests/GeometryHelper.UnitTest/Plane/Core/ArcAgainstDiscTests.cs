using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// <c>DistanceTo</c> reads a circle as the disc it bounds, from an arc and from an edge as from anything else.
    /// </summary>
    /// <remarks>
    /// An arc measured a circle as another arc, its rim: an arc lying inside a disc of radius 100 was 70 away
    /// while CollidesWith said the two touched, and a straight edge inside it was 80 away from the edge's side
    /// and nought from the circle's. A segment always read the disc. GetShortestLineTo still measures rim to
    /// rim, as it does for every closed shape.
    /// </remarks>
    public class ArcAgainstDiscTests
    {
        private static readonly GeoCircle2 Disc = new GeoCircle2(new GeoPoint2(0, 0), 100);

        [Fact]
        public void AnArcPassingFourTenThousandthsOutsideTheDisc_TouchesIt_AsItsDistanceSays()
        {
            // The left half of a circle of radius 101.5004 about (1.5, 0) passes the disc of radius 100 at (-100, 0) only
            // 0.0004 out, within the point tolerance of 0.001: DistanceTo says 0.0004, so it touches, at the point between
            // the two rims, (-100.0002, 0). The touch was read off the radical line of the two circles at (-100.027, 0),
            // 0.027 off the arc, so the arc did not reach it and the two neither met nor collided.
            var tolerance = new Tolerance(1E-3, 1E-5);
            var arc = new GeoArc2(new GeoPoint2(1.5, 0), 101.5004, Math.PI / 2, 3 * Math.PI / 2);

            Assert.Equal(0.0004, Disc.DistanceTo(arc, tolerance), 9);
            Assert.True(Disc.CollidesWith(arc, tolerance));
            Assert.True(arc.CollidesWith(Disc, tolerance));

            GeoPoint2 touch = Assert.Single(Disc.GetIntersections(arc, tolerance));
            Assert.Equal(-100.0002, touch.X, 9);
            Assert.Equal(0.0, touch.Y, 9);
        }

        [Fact]
        public void AnArcInsideTheDiscIsNoughtAwayFromEitherSide()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 30, 0, Math.PI);

            Assert.Equal(0.0, arc.DistanceTo(Disc), 9);
            Assert.Equal(0.0, Disc.DistanceTo(arc), 9);
            Assert.True(arc.CollidesWith(Disc));
        }

        [Fact]
        public void AnEdgeInsideTheDiscIsNoughtAwayFromEitherSideWhetherStraightOrCurved()
        {
            var straight = new GeoEdge2(new GeoPoint2(-20, 0), new GeoPoint2(20, 5));
            var curved = new GeoEdge2(new GeoArc2(new GeoPoint2(0, 0), 30, 0, Math.PI));

            foreach (GeoEdge2 edge in new[] { straight, curved })
            {
                Assert.Equal(0.0, edge.DistanceTo(Disc), 9);
                Assert.Equal(0.0, Disc.DistanceTo(edge), 9);
                Assert.True(edge.CollidesWith(Disc));
            }
        }

        [Fact]
        public void OutsideTheDiscTheReachIsToTheRim()
        {
            // Facing the disc: the nearest point is straight towards the centre.
            var facing = new GeoArc2(new GeoPoint2(200, 0), 30, Math.PI / 2, 3 * Math.PI / 2);
            Assert.Equal(70.0, facing.DistanceTo(Disc), 9);

            // Turned away: the nearest point is an end.
            var away = new GeoArc2(new GeoPoint2(200, 0), 30, -Math.PI / 3, Math.PI / 3);
            double end = Math.Sqrt(215.0 * 215.0 + 675.0);
            Assert.Equal(end - 100.0, away.DistanceTo(Disc), 9);
            Assert.Equal(end - 100.0, Disc.DistanceTo(away), 9);
        }

        [Fact]
        public void TheShortestLineStillRunsRimToRim()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 30, 0, Math.PI);

            Assert.Equal(70.0, arc.GetShortestLineTo(Disc).Length, 9);
        }

        [Fact]
        public void AnArcRoundASmallCircleIsNearestItOnTheFarSide()
        {
            // The small circle lies inside the arc's own circle, so the two are nearest on the same side of the
            // line joining the centres, not facing each other. The gap is 58.249 - 40.057 - 16.99.
            var circle = new GeoCircle2(new GeoPoint2(40.743, -71.188), 16.99);
            var arc = new GeoArc2(new GeoPoint2(41.028, -31.132), 58.249, 258.7 * Math.PI / 180, 299.0 * Math.PI / 180);
            double gap = 58.249 - new GeoPoint2(40.743, -71.188).DistanceTo(new GeoPoint2(41.028, -31.132)) - 16.99;

            Assert.Equal(gap, arc.DistanceTo(circle), 9);
            Assert.Equal(gap, arc.GetShortestLineTo(circle).Length, 9);
            Assert.Equal(gap, circle.GetShortestLineTo(arc).Length, 9);

            // And the same for two arcs, either way round.
            var inner = new GeoArc2(circle.Center, circle.Radius, -Math.PI, 0);
            Assert.Equal(gap, arc.DistanceTo(inner), 9);
            Assert.Equal(gap, inner.DistanceTo(arc), 9);
            Assert.Equal(gap, inner.GetShortestLineTo(arc).Length, 9);
        }

        [Fact]
        public void RandomArcsAgreeWithCollidesWithAndWithTheReverse()
        {
            var rng = new Random(4711);

            for (int k = 0; k < 500; k++)
            {
                var circle = new GeoCircle2(new GeoPoint2(rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100), 5 + rng.NextDouble() * 80);
                double start = rng.NextDouble() * 2 * Math.PI;
                var arc = new GeoArc2(
                    new GeoPoint2(rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100),
                    5 + rng.NextDouble() * 80,
                    start,
                    start + 0.2 + rng.NextDouble() * 5.5);
                var edge = new GeoEdge2(arc);
                var chord = new GeoEdge2(arc.StartPoint, arc.EndPoint);

                double distance = arc.DistanceTo(circle);

                Assert.Equal(distance, circle.DistanceTo(arc), 9);
                Assert.Equal(distance, edge.DistanceTo(circle), 9);
                Assert.Equal(chord.DistanceTo(circle), circle.DistanceTo(chord), 9);
                Assert.Equal(distance <= 1E-9, arc.CollidesWith(circle));

                // Outside the disc the shortest line, which runs rim to rim, is as long as the distance.
                if (distance > 1E-9)
                {
                    Assert.Equal(distance, arc.GetShortestLineTo(circle).Length, 9);
                }
                Assert.Equal(chord.DistanceTo(circle) <= 1E-9, chord.CollidesWith(circle));
            }
        }
    }
}
