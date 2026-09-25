using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The segment joining an arc to something else. The library could already say how far apart the two
    /// were; this says between which two points, and the promise that binds the new answer to the old is
    /// that the segment is exactly as long as the distance was.
    /// </summary>
    public class ArcShortestLineTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// The right-hand half of a circle of radius fifty about the origin.
        /// </summary>
        private static GeoArc2 RightHalf() => new GeoArc2(new GeoPoint2(0, 0), 50.0, -Math.PI / 2, Math.PI / 2);

        private static bool OnArc(GeoArc2 arc, GeoPoint2 point) => Arc2.IsPointOn(arc, point, Tight);

        private static bool OnLine(GeoLine2 line, GeoPoint2 point) => Distance2.DistanceTo(line, point) < 1E-7;

        [Fact]
        public void TheSegmentToAPointLeavesTheArcAndLandsOnThePoint()
        {
            GeoArc2 arc = RightHalf();
            var point = new GeoPoint2(200, 0);

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, point);

            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint2(50, 0), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(point, Tight));
            Assert.Equal(Arc2.DistanceTo(arc, point), joining.Length, 9);
            Assert.Equal(150.0, joining.Length, 9);
        }

        [Fact]
        public void TheSegmentToALineIsSquareToItWhenTheArcReachesRound()
        {
            // A vertical segment out at x = 200, level with the middle of the half circle, so the nearest
            // pair is the arc at (50, 0) against the line at (200, 0).
            GeoArc2 arc = RightHalf();
            var line = new GeoLine2(new GeoPoint2(200, -40), new GeoPoint2(200, 40));

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, line);

            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint2(50, 0), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(new GeoPoint2(200, 0), Tight));
            Assert.Equal(150.0, joining.Length, 9);
            Assert.Equal(Arc2.DistanceTo(arc, line), joining.Length, 9);
        }

        [Fact]
        public void WhereTheArcDoesNotReachRoundTheNearestPairFallsBackToAnEnd()
        {
            // The same half circle, but the segment sits away behind it, on the side the arc turns its
            // back on. The nearest pair is then an end of the arc, not the point facing the centre.
            GeoArc2 arc = RightHalf();
            var line = new GeoLine2(new GeoPoint2(-200, -40), new GeoPoint2(-200, 40));

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, line);

            Assert.True(OnArc(arc, joining.StartPoint));
            Assert.True(OnLine(line, joining.EndPoint));
            Assert.Equal(Arc2.DistanceTo(arc, line), joining.Length, 9);

            // Both ends of the arc stand the same distance off, and the earlier one is kept.
            Assert.True(joining.StartPoint.IsEqualTo(arc.StartPoint, Tight));
        }

        [Fact]
        public void ShapesThatCrossAreJoinedByNothingAtAll()
        {
            GeoArc2 arc = RightHalf();
            var through = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(200, 0));

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, through);

            Assert.Equal(0.0, joining.Length, 12);
            Assert.True(joining.StartPoint.IsEqualTo(joining.EndPoint, Tight));
            Assert.True(OnArc(arc, joining.StartPoint));
            Assert.True(OnLine(through, joining.StartPoint));
            Assert.Equal(0.0, Arc2.DistanceTo(arc, through), 12);
        }

        [Fact]
        public void TwoArcsAreJoinedAlongTheLineBetweenTheirCentres()
        {
            GeoArc2 arc = RightHalf();

            // A second half circle of radius twenty about (200, 0), turning its back the other way, so the
            // two face each other across the gap.
            var facing = new GeoArc2(new GeoPoint2(200, 0), 20.0, Math.PI / 2, 3 * Math.PI / 2);

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, facing);

            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint2(50, 0), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(new GeoPoint2(180, 0), Tight));
            Assert.Equal(130.0, joining.Length, 9);
            Assert.Equal(Arc2.DistanceTo(arc, facing), joining.Length, 9);
        }

        [Fact]
        public void AnArcAndACircleAreJoinedTheWayTwoArcsAre()
        {
            GeoArc2 arc = RightHalf();
            var circle = new GeoCircle2(new GeoPoint2(200, 0), 20.0);

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, circle);

            Assert.Equal(130.0, joining.Length, 9);
            Assert.Equal(Arc2.DistanceTo(arc, circle), joining.Length, 9);
            Assert.True(OnArc(arc, joining.StartPoint));
            Assert.Equal(circle.Radius, circle.Center.DistanceTo(joining.EndPoint), 9);
        }

        [Fact]
        public void TheDefaultToleranceFormAgreesWithTheOneThatIsGivenOne()
        {
            GeoArc2 arc = RightHalf();
            var point = new GeoPoint2(120, 30);
            var line = new GeoLine2(new GeoPoint2(200, -40), new GeoPoint2(200, 40));
            var other = new GeoArc2(new GeoPoint2(200, 0), 20.0, Math.PI / 2, 3 * Math.PI / 2);
            var circle = new GeoCircle2(new GeoPoint2(200, 0), 20.0);

            Assert.True(Arc2.GetShortestLineTo(arc, point).IsEqualTo(Arc2.GetShortestLineTo(arc, point, Tolerance.Global), Tight));
            Assert.True(Arc2.GetShortestLineTo(arc, line).IsEqualTo(Arc2.GetShortestLineTo(arc, line, Tolerance.Global), Tight));
            Assert.True(Arc2.GetShortestLineTo(arc, other).IsEqualTo(Arc2.GetShortestLineTo(arc, other, Tolerance.Global), Tight));
            Assert.True(Arc2.GetShortestLineTo(arc, circle).IsEqualTo(Arc2.GetShortestLineTo(arc, circle, Tolerance.Global), Tight));
        }

        /// <summary>
        /// The binding promise, over a great many shapes at once: the segment is exactly as long as the
        /// distance already measured, and each of its ends really does sit on the shape it came from.
        /// </summary>
        [Fact]
        public void OverManyShapesTheSegmentIsAsLongAsTheDistanceAndItsEndsAreWhereTheyShouldBe()
        {
            var random = new Random(20260925);
            var arcs = new List<GeoArc2>();
            var lines = new List<GeoLine2>();

            for (int i = 0; i < 40; i++)
            {
                double radius = 5.0 + random.NextDouble() * 120.0;
                double start = random.NextDouble() * Math.PI * 2;
                double swept = (random.NextDouble() * 2.0 - 1.0) * Math.PI * 1.9;

                arcs.Add(new GeoArc2(
                    new GeoPoint2(random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200),
                    radius, start, start + swept));

                lines.Add(new GeoLine2(
                    new GeoPoint2(random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200),
                    new GeoPoint2(random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200)));
            }

            int crossings = 0;

            foreach (GeoArc2 arc in arcs)
            {
                foreach (GeoLine2 line in lines)
                {
                    GeoLine2 joining = Arc2.GetShortestLineTo(arc, line);
                    double apart = Arc2.DistanceTo(arc, line);

                    Assert.Equal(apart, joining.Length, 7);
                    Assert.True(OnArc(arc, joining.StartPoint), "start off the arc");
                    Assert.True(OnLine(line, joining.EndPoint), "end off the segment");

                    if (apart <= 0.0)
                    {
                        crossings++;
                    }
                }

                foreach (GeoArc2 other in arcs)
                {
                    GeoLine2 joining = Arc2.GetShortestLineTo(arc, other);

                    Assert.Equal(Arc2.DistanceTo(arc, other), joining.Length, 7);
                    Assert.True(OnArc(arc, joining.StartPoint), "start off the first arc");
                    Assert.True(OnArc(other, joining.EndPoint), "end off the second arc");
                }
            }

            // The sweep is worth nothing if none of the pairs ever met; these do.
            Assert.True(crossings > 20, "only " + crossings + " crossings in the sweep");
        }
    }
}
