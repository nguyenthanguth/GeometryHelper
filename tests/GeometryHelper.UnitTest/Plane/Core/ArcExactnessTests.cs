using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Whether an arc reaches a point of its own circle used to be settled by comparing two directions
    /// within <see cref="Tolerance.EqualAngleRad"/>, a whole degree by default. A degree of a large arc is
    /// a long way: on a radius of a hundred it is nearly two of whatever the drawing is measured in. Both
    /// shapes here were turned up by a sweep, and both are held to a brute-force reading of the same
    /// geometry rather than to a number written down once.
    /// </summary>
    public class ArcExactnessTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// An arc that turns its back on a far-off segment. The point of its circle facing that segment
        /// lies a third of a degree past the end of the arc.
        /// </summary>
        private static GeoArc2 TurnedAway() => new GeoArc2(
            new GeoPoint2(106.613242210175, 167.196175999565),
            105.215687332775,
            2.65100934993475,
            2.65100934993475 + 0.540859308513576);

        private static GeoLine2 FarOff() => new GeoLine2(
            new GeoPoint2(-125.347395392762, -167.432944275175),
            new GeoPoint2(-140.12959969236, 153.860483297082));

        /// <summary>
        /// Walks an arc and reports the least distance any of its points stands from a segment.
        /// </summary>
        private static double NearestByWalking(GeoArc2 arc, GeoLine2 line, int steps)
        {
            double nearest = double.MaxValue;

            for (int i = 0; i <= steps; i++)
            {
                nearest = Math.Min(nearest, Distance2.DistanceTo(line, arc.GetPointAtParameter((double)i / steps)));
            }

            return nearest;
        }

        [Fact]
        public void AnArcIsNeverReportedNearerThanItsOwnPointsAllow()
        {
            GeoArc2 arc = TurnedAway();
            GeoLine2 line = FarOff();

            double reported = Arc2.DistanceTo(arc, line);
            double walked = NearestByWalking(arc, line, 20000);

            // No point of the arc is as near as the reported distance would need one to be.
            Assert.True(reported <= walked + 1E-9, "reported " + reported + " against " + walked);
            Assert.Equal(walked, reported, 6);

            // The nearest point of this arc is its own end, so the answer is that end exactly.
            Assert.Equal(Distance2.DistanceTo(line, arc.EndPoint), reported, 9);
        }

        [Fact]
        public void TheSegmentJoiningThemLeavesTheArcAtItsEnd()
        {
            GeoArc2 arc = TurnedAway();
            GeoLine2 line = FarOff();

            GeoLine2 joining = Arc2.GetShortestLineTo(arc, line);

            Assert.True(Arc2.IsPointOn(arc, joining.StartPoint, Tight));
            Assert.True(joining.StartPoint.IsEqualTo(arc.EndPoint, Tight));
            Assert.Equal(Arc2.DistanceTo(arc, line), joining.Length, 9);
        }

        [Fact]
        public void ACrossingOfTheCirclesCountsOnlyWhereBothArcsReachIt()
        {
            var first = new GeoArc2(
                new GeoPoint2(-174.945561389879, 174.317677493355),
                64.4248551779542,
                1.74034633861525,
                1.74034633861525 + 5.70359477699884);

            var second = new GeoArc2(
                new GeoPoint2(-114.858829562952, 66.2477894994653),
                112.608687368971,
                1.54518810745785,
                1.54518810745785 + 3.35263869779607);

            // A whole turn is an arc like any other, so the circles carrying these two can be asked the
            // same question. They cross twice.
            GeoPoint2[] onTheCircles = Arc2.GetIntersections(
                new GeoArc2(first.Center, first.Radius, 0.0, 0.0),
                new GeoArc2(second.Center, second.Radius, 0.0, 0.0));

            Assert.Equal(2, onTheCircles.Length);

            // The arcs themselves cross once.
            GeoPoint2[] onTheArcs = Arc2.GetIntersections(first, second);

            Assert.Single(onTheArcs);
            Assert.True(Arc2.IsPointOn(first, onTheArcs[0], Tight), "off the first arc");
            Assert.True(Arc2.IsPointOn(second, onTheArcs[0], Tight), "off the second arc");

            // The other crossing is the one that used to be let through: it stands more than a unit clear
            // of the second arc, and yet within a degree of its end, which is the whole of the window the
            // angular test opened.
            GeoPoint2 rejected = onTheCircles.Single(meeting => !Arc2.IsPointOn(second, meeting, Tight));
            double past = Arc2.DistanceTo(second, rejected);

            Assert.True(past > 1.0, "only " + past + " past the end");
            Assert.True(past < second.Radius * Math.PI / 180.0, past + " is further than a degree");

            // They do cross, so the gap is nothing, and the joining segment says the same.
            Assert.Equal(0.0, Arc2.DistanceTo(first, second), 9);
            Assert.Equal(0.0, Arc2.GetShortestLineTo(first, second).Length, 9);
        }

        [Fact]
        public void EveryReportedMeetingLiesOnBothShapes()
        {
            var random = new Random(20260925);
            var arcs = new GeoArc2[60];
            var lines = new GeoLine2[60];

            for (int i = 0; i < arcs.Length; i++)
            {
                double start = random.NextDouble() * Math.PI * 2;

                arcs[i] = new GeoArc2(
                    new GeoPoint2(random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150),
                    5.0 + random.NextDouble() * 120.0,
                    start,
                    start + (random.NextDouble() * 2.0 - 1.0) * Math.PI * 1.9);

                lines[i] = new GeoLine2(
                    new GeoPoint2(random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150),
                    new GeoPoint2(random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150));
            }

            int found = 0;

            foreach (GeoArc2 arc in arcs)
            {
                foreach (GeoLine2 line in lines)
                {
                    foreach (GeoPoint2 meeting in Arc2.GetIntersections(arc, line))
                    {
                        Assert.True(Arc2.IsPointOn(arc, meeting, Tight), "meeting off the arc");
                        Assert.True(Distance2.DistanceTo(line, meeting) < 1E-7, "meeting off the segment");
                        found++;
                    }
                }

                foreach (GeoArc2 other in arcs)
                {
                    foreach (GeoPoint2 meeting in Arc2.GetIntersections(arc, other))
                    {
                        Assert.True(Arc2.IsPointOn(arc, meeting, Tight), "meeting off the first arc");
                        Assert.True(Arc2.IsPointOn(other, meeting, Tight), "meeting off the second arc");
                        found++;
                    }
                }
            }

            Assert.True(found > 200, "only " + found + " meetings in the sweep");
        }
    }
}
