using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The segment joining a chain or loop that may curve to something else. Until now those two types
    /// could say how far off a thing was but not where it was, which is why the name was taken for
    /// something else and had to be given back.
    /// </summary>
    public class ArcChainShortestLineTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A square of side one hundred whose right-hand side swells into a half circle reaching x = 150.
        /// </summary>
        private static GeoPolygonArc2 Slot() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
            new[] { 0.0, 1.0, 0.0, 0.0 });

        private static GeoPolylineArc2 OpenSlot() => Slot().ToPolylineArc2();

        private static bool OnBoundary(GeoPolygonArc2 loop, GeoPoint2 point)
            => loop.GetClosestPointOnBoundary(point).DistanceTo(point) < 1E-7;

        private static bool OnBoundary(GeoPolylineArc2 chain, GeoPoint2 point)
            => chain.GetClosestPointOnBoundary(point).DistanceTo(point) < 1E-7;

        [Fact]
        public void TheSegmentLeavesTheBulgeAndLandsOnWhatIsAskedAbout()
        {
            GeoPolygonArc2 slot = Slot();

            // A short segment out at x = 300, level with the middle, so the bulge faces it squarely.
            var line = new GeoLine2(new GeoPoint2(300, 40), new GeoPoint2(300, 60));

            GeoLine2 joining = slot.GetShortestLineTo(line);

            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint2(150, 50), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(new GeoPoint2(300, 50), Tight));
            Assert.Equal(150.0, joining.Length, 9);
            Assert.Equal(slot.DistanceTo(line), joining.Length, 9);
        }

        [Fact]
        public void EverySortOfShapeIsJoinedAndTheLengthIsTheDistanceAlreadyMeasured()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();

            var line = new GeoLine2(new GeoPoint2(400, 0), new GeoPoint2(400, 100));
            var arc = new GeoArc2(new GeoPoint2(400, 50), 20.0, Math.PI / 2, 3 * Math.PI / 2);
            var circle = new GeoCircle2(new GeoPoint2(400, 50), 20.0);
            var polygon = new GeoPolygon2(
                new GeoPoint2(400, 0), new GeoPoint2(500, 0), new GeoPoint2(500, 100), new GeoPoint2(400, 100));
            var polyline = new GeoPolyline2(new GeoPoint2(400, 0), new GeoPoint2(400, 100));
            var otherLoop = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(400, 0), new GeoPoint2(500, 0), new GeoPoint2(500, 100), new GeoPoint2(400, 100)));
            var otherChain = otherLoop.ToPolylineArc2();

            Assert.Equal(slot.DistanceTo(line), slot.GetShortestLineTo(line).Length, 9);
            Assert.Equal(slot.DistanceTo(arc), slot.GetShortestLineTo(arc).Length, 9);
            Assert.Equal(slot.DistanceTo(circle), slot.GetShortestLineTo(circle).Length, 9);
            Assert.Equal(slot.DistanceTo(polygon), slot.GetShortestLineTo(polygon).Length, 9);
            Assert.Equal(slot.DistanceTo(polyline), slot.GetShortestLineTo(polyline).Length, 9);
            Assert.Equal(slot.DistanceTo(otherLoop), slot.GetShortestLineTo(otherLoop).Length, 9);
            Assert.Equal(slot.DistanceTo(otherChain), slot.GetShortestLineTo(otherChain).Length, 9);

            Assert.Equal(open.DistanceTo(line), open.GetShortestLineTo(line).Length, 9);
            Assert.Equal(open.DistanceTo(arc), open.GetShortestLineTo(arc).Length, 9);
            Assert.Equal(open.DistanceTo(circle), open.GetShortestLineTo(circle).Length, 9);
            Assert.Equal(open.DistanceTo(polygon), open.GetShortestLineTo(polygon).Length, 9);
            Assert.Equal(open.DistanceTo(polyline), open.GetShortestLineTo(polyline).Length, 9);
            Assert.Equal(open.DistanceTo(otherLoop), open.GetShortestLineTo(otherLoop).Length, 9);
            Assert.Equal(open.DistanceTo(otherChain), open.GetShortestLineTo(otherChain).Length, 9);

            // Every one of them leaves the shape it was asked of.
            foreach (GeoLine2 joining in new[]
            {
                slot.GetShortestLineTo(line), slot.GetShortestLineTo(arc), slot.GetShortestLineTo(circle),
                slot.GetShortestLineTo(polygon), slot.GetShortestLineTo(polyline),
                slot.GetShortestLineTo(otherLoop), slot.GetShortestLineTo(otherChain)
            })
            {
                Assert.True(OnBoundary(slot, joining.StartPoint), "start off the loop");
            }

            foreach (GeoLine2 joining in new[]
            {
                open.GetShortestLineTo(line), open.GetShortestLineTo(arc), open.GetShortestLineTo(circle),
                open.GetShortestLineTo(polygon), open.GetShortestLineTo(polyline),
                open.GetShortestLineTo(otherLoop), open.GetShortestLineTo(otherChain)
            })
            {
                Assert.True(OnBoundary(open, joining.StartPoint), "start off the chain");
            }
        }

        [Fact]
        public void AShapeInsideAClosedOneIsMeasuredToTheOutlineNotToNothing()
        {
            // This is where the segment and the distance part company on purpose: Distance2 reads a closed
            // shape as a filled region, so a circle inside it is nought away. The segment runs between two
            // boundaries and has a length whatever sits inside what.
            GeoPolygonArc2 slot = Slot();
            var inside = new GeoCircle2(new GeoPoint2(50, 50), 10.0);

            Assert.Equal(0.0, slot.DistanceTo(inside), 12);

            GeoLine2 joining = slot.GetShortestLineTo(inside);

            // The nearest side is the bottom, fifty away, less the radius.
            Assert.Equal(40.0, joining.Length, 9);
            Assert.True(OnBoundary(slot, joining.StartPoint));
            Assert.Equal(inside.Radius, inside.Center.DistanceTo(joining.EndPoint), 9);
        }

        [Fact]
        public void BoundariesThatCrossAreJoinedByNothingAtAll()
        {
            GeoPolygonArc2 slot = Slot();
            var knife = new GeoLine2(new GeoPoint2(-50, 50), new GeoPoint2(400, 50));

            GeoLine2 joining = slot.GetShortestLineTo(knife);

            Assert.Equal(0.0, joining.Length, 9);
            Assert.True(joining.StartPoint.IsEqualTo(joining.EndPoint, Tight));
            Assert.True(OnBoundary(slot, joining.StartPoint));
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();
            var line = new GeoLine2(new GeoPoint2(400, 0), new GeoPoint2(400, 100));
            var polygon = new GeoPolygon2(
                new GeoPoint2(400, 0), new GeoPoint2(500, 0), new GeoPoint2(500, 100), new GeoPoint2(400, 100));

            Assert.True(Projection2.GetShortestLineTo(slot, line).IsEqualTo(slot.GetShortestLineTo(line), Tight));
            Assert.True(Projection2.GetShortestLineTo(slot, line, Tolerance.Global).IsEqualTo(slot.GetShortestLineTo(line, Tolerance.Global), Tight));
            Assert.True(Projection2.GetShortestLineTo(slot, polygon).IsEqualTo(slot.GetShortestLineTo(polygon), Tight));
            Assert.True(Projection2.GetShortestLineTo(open, line).IsEqualTo(open.GetShortestLineTo(line), Tight));
            Assert.True(Projection2.GetShortestLineTo(open, polygon, Tolerance.Global).IsEqualTo(open.GetShortestLineTo(polygon, Tolerance.Global), Tight));

            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo((GeoPolygonArc2)null, line));
            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo((GeoPolylineArc2)null, line));
            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo(slot, (GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo(open, (GeoPolylineArc2)null, Tolerance.Global));
        }

        [Fact]
        public void TheAnswerIsTheSameWhicheverEndTheQuestionStartsFrom()
        {
            // The segment turns round, but it joins the same two points.
            GeoPolygonArc2 slot = Slot();
            var otherLoop = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(400, 0), new GeoPoint2(500, 0), new GeoPoint2(500, 100), new GeoPoint2(400, 100)));

            GeoLine2 there = slot.GetShortestLineTo(otherLoop);
            GeoLine2 back = otherLoop.GetShortestLineTo(slot);

            Assert.Equal(there.Length, back.Length, 9);
            Assert.True(there.StartPoint.IsEqualTo(back.EndPoint, Tight));
            Assert.True(there.EndPoint.IsEqualTo(back.StartPoint, Tight));
        }
    }
}
