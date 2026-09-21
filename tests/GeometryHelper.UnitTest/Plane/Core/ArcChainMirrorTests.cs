using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Every way of asking a curved chain the same question. The library offers each operation twice —
    /// once as a static naming the larger shape first, once as a method on the shape — and the second has
    /// to hand the first exactly what it was given. A mirror wired to the wrong static is the whole class
    /// of mistake this catches, and there is no way to see it except by calling both.
    /// </summary>
    public class ArcChainMirrorTests
    {
        private static GeoPolygonArc2 Loop() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
            new[] { 0.0, 1.0, 0.0, 1.0 });

        private static GeoPolylineArc2 Chain() => new GeoPolylineArc2(
            new[] { new GeoPoint2(-300, 10), new GeoPoint2(-100, 10), new GeoPoint2(-100, 120) },
            new[] { 0.0, 0.4 });

        private static GeoLine2 Line() => new GeoLine2(new GeoPoint2(100, -80), new GeoPoint2(100, 140));

        private static GeoArc2 Arc() => new GeoArc2(new GeoPoint2(100, 25), 120.0, 0.0, Math.PI);

        private static GeoCircle2 Circle() => new GeoCircle2(new GeoPoint2(100, 25), 140.0);

        private static GeoPolyline2 Straight() => new GeoPolyline2(
            new GeoPoint2(-50, -60), new GeoPoint2(250, -60), new GeoPoint2(250, 160));

        private static GeoPolygon2 Plate() => new GeoPolygon2(
            new GeoPoint2(60, 10), new GeoPoint2(140, 10), new GeoPoint2(140, 40), new GeoPoint2(60, 40));

        [Fact]
        public void EveryMeasurementOnALoopReadsTheSameBothWays()
        {
            GeoPolygonArc2 loop = Loop();

            Assert.Equal(Distance2.DistanceTo(loop, Line()), loop.DistanceTo(Line()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Arc()), loop.DistanceTo(Arc()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Circle()), loop.DistanceTo(Circle()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Straight()), loop.DistanceTo(Straight()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Plate()), loop.DistanceTo(Plate()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Chain()), loop.DistanceTo(Chain()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, Loop()), loop.DistanceTo(Loop()), 12);
            Assert.Equal(Distance2.DistanceTo(loop, new GeoPoint2(400, 25)), loop.DistanceTo(new GeoPoint2(400, 25)), 12);

            Assert.Equal(Intersection2.GetIntersections(loop, Line()).Length, loop.GetIntersections(Line()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Arc()).Length, loop.GetIntersections(Arc()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Circle()).Length, loop.GetIntersections(Circle()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Straight()).Length, loop.GetIntersections(Straight()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Plate()).Length, loop.GetIntersections(Plate()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Chain()).Length, loop.GetIntersections(Chain()).Length);
            Assert.Equal(Intersection2.GetIntersections(loop, Loop()).Length, loop.GetIntersections(Loop()).Length);

            Assert.Equal(Collision2.CollidesWith(loop, Line()), loop.CollidesWith(Line()));
            Assert.Equal(Collision2.CollidesWith(loop, Arc()), loop.CollidesWith(Arc()));
            Assert.Equal(Collision2.CollidesWith(loop, Circle()), loop.CollidesWith(Circle()));
            Assert.Equal(Collision2.CollidesWith(loop, Straight()), loop.CollidesWith(Straight()));
            Assert.Equal(Collision2.CollidesWith(loop, Plate()), loop.CollidesWith(Plate()));
            Assert.Equal(Collision2.CollidesWith(loop, Chain()), loop.CollidesWith(Chain()));
            Assert.Equal(Collision2.CollidesWith(loop, Loop()), loop.CollidesWith(Loop()));

            Tolerance global = Tolerance.Global;

            Assert.Equal(loop.DistanceTo(Straight()), loop.DistanceTo(Straight(), global), 12);
            Assert.Equal(loop.DistanceTo(Plate()), loop.DistanceTo(Plate(), global), 12);
            Assert.Equal(loop.DistanceTo(Chain()), loop.DistanceTo(Chain(), global), 12);
            Assert.Equal(loop.DistanceTo(Loop()), loop.DistanceTo(Loop(), global), 12);
            Assert.Equal(loop.GetIntersections(Arc()).Length, loop.GetIntersections(Arc(), global).Length);
            Assert.Equal(loop.GetIntersections(Straight()).Length, loop.GetIntersections(Straight(), global).Length);
            Assert.Equal(loop.GetIntersections(Plate()).Length, loop.GetIntersections(Plate(), global).Length);
            Assert.Equal(loop.GetIntersections(Chain()).Length, loop.GetIntersections(Chain(), global).Length);
            Assert.Equal(loop.GetIntersections(Loop()).Length, loop.GetIntersections(Loop(), global).Length);
            Assert.Equal(loop.CollidesWith(Arc()), loop.CollidesWith(Arc(), global));
            Assert.Equal(loop.CollidesWith(Circle()), loop.CollidesWith(Circle(), global));
            Assert.Equal(loop.CollidesWith(Straight()), loop.CollidesWith(Straight(), global));
            Assert.Equal(loop.CollidesWith(Plate()), loop.CollidesWith(Plate(), global));
            Assert.Equal(loop.CollidesWith(Chain()), loop.CollidesWith(Chain(), global));
            Assert.Equal(loop.CollidesWith(Loop()), loop.CollidesWith(Loop(), global));
        }

        [Fact]
        public void EveryMeasurementOnAChainReadsTheSameBothWays()
        {
            GeoPolylineArc2 chain = Chain();

            Assert.Equal(Distance2.DistanceTo(chain, Line()), chain.DistanceTo(Line()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Arc()), chain.DistanceTo(Arc()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Circle()), chain.DistanceTo(Circle()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Straight()), chain.DistanceTo(Straight()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Plate()), chain.DistanceTo(Plate()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Loop()), chain.DistanceTo(Loop()), 12);
            Assert.Equal(Distance2.DistanceTo(chain, Chain()), chain.DistanceTo(Chain()), 12);

            Assert.Equal(Intersection2.GetIntersections(chain, Line()).Length, chain.GetIntersections(Line()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Arc()).Length, chain.GetIntersections(Arc()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Circle()).Length, chain.GetIntersections(Circle()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Straight()).Length, chain.GetIntersections(Straight()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Plate()).Length, chain.GetIntersections(Plate()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Chain()).Length, chain.GetIntersections(Chain()).Length);
            Assert.Equal(Intersection2.GetIntersections(chain, Loop()).Length, chain.GetIntersections(Loop()).Length);

            Assert.Equal(Collision2.CollidesWith(chain, Line()), chain.CollidesWith(Line()));
            Assert.Equal(Collision2.CollidesWith(chain, Arc()), chain.CollidesWith(Arc()));
            Assert.Equal(Collision2.CollidesWith(chain, Circle()), chain.CollidesWith(Circle()));
            Assert.Equal(Collision2.CollidesWith(chain, Straight()), chain.CollidesWith(Straight()));
            Assert.Equal(Collision2.CollidesWith(chain, Plate()), chain.CollidesWith(Plate()));
            Assert.Equal(Collision2.CollidesWith(chain, Chain()), chain.CollidesWith(Chain()));
            Assert.Equal(Collision2.CollidesWith(chain, Loop()), chain.CollidesWith(Loop()));

            // And the tolerance form of each, which is the overload a caller reaches for last and so the
            // one most likely to have been wired to the wrong static.
            Tolerance global = Tolerance.Global;

            Assert.Equal(chain.DistanceTo(Arc()), chain.DistanceTo(Arc(), global), 12);
            Assert.Equal(chain.DistanceTo(Straight()), chain.DistanceTo(Straight(), global), 12);
            Assert.Equal(chain.DistanceTo(Plate()), chain.DistanceTo(Plate(), global), 12);
            Assert.Equal(chain.DistanceTo(Chain()), chain.DistanceTo(Chain(), global), 12);
            Assert.Equal(chain.DistanceTo(Loop()), chain.DistanceTo(Loop(), global), 12);
            Assert.Equal(chain.DistanceTo(Line()), chain.DistanceTo(Line(), global), 12);

            Assert.Equal(chain.GetIntersections(Arc()).Length, chain.GetIntersections(Arc(), global).Length);
            Assert.Equal(chain.GetIntersections(Circle()).Length, chain.GetIntersections(Circle(), global).Length);
            Assert.Equal(chain.GetIntersections(Straight()).Length, chain.GetIntersections(Straight(), global).Length);
            Assert.Equal(chain.GetIntersections(Plate()).Length, chain.GetIntersections(Plate(), global).Length);
            Assert.Equal(chain.GetIntersections(Chain()).Length, chain.GetIntersections(Chain(), global).Length);
            Assert.Equal(chain.GetIntersections(Loop()).Length, chain.GetIntersections(Loop(), global).Length);

            Assert.Equal(chain.CollidesWith(Arc()), chain.CollidesWith(Arc(), global));
            Assert.Equal(chain.CollidesWith(Circle()), chain.CollidesWith(Circle(), global));
            Assert.Equal(chain.CollidesWith(Straight()), chain.CollidesWith(Straight(), global));
            Assert.Equal(chain.CollidesWith(Plate()), chain.CollidesWith(Plate(), global));
            Assert.Equal(chain.CollidesWith(Chain()), chain.CollidesWith(Chain(), global));
            Assert.Equal(chain.CollidesWith(Loop()), chain.CollidesWith(Loop(), global));

            // The chain crosses the ring it sits beside nowhere, and touches nothing it is apart from.
            Assert.Empty(chain.GetIntersections(Loop()));
            Assert.False(chain.CollidesWith(Loop()));
            Assert.True(chain.DistanceTo(Loop()) > 0.0);
        }

        [Fact]
        public void TheAnswersThemselvesAreRightAndNotMerelyConsistent()
        {
            GeoPolygonArc2 loop = Loop();

            // The slot reaches x = 225 at its right hand end, so a point at 400 is 175 away.
            Assert.Equal(175.0, loop.DistanceTo(new GeoPoint2(400, 25)), 8);

            // A line straight down the middle cuts both long sides.
            Assert.Equal(2, loop.GetIntersections(Line()).Length);
            Assert.True(loop.CollidesWith(Line()));

            // A plate lying wholly inside the slot touches nothing but is not apart from it.
            Assert.Empty(loop.GetIntersections(Plate()));
            Assert.True(loop.CollidesWith(Plate()));
            Assert.Equal(0.0, loop.DistanceTo(Plate()), 12);

            // A chain well away from it meets nothing and is measured across the gap.
            Assert.Empty(loop.GetIntersections(Chain()));
            Assert.False(loop.CollidesWith(Chain()));
            Assert.True(loop.DistanceTo(Chain()) > 50.0);

            // A circle drawn round the slot holds it without touching.
            Assert.Empty(loop.GetIntersections(Circle()));
            Assert.True(loop.CollidesWith(Circle()));
        }

        [Fact]
        public void WalkingAndProjectingReadTheSameBothWays()
        {
            GeoPolygonArc2 loop = Loop();
            var point = new GeoPoint2(230, 40);

            Assert.Equal(Parametrization2.GetPointAtDistance(loop, 260.0).X, loop.GetPointAtDistance(260.0).X, 12);
            Assert.Equal(Parametrization2.GetPointAtParameter(loop, 0.3).Y, loop.GetPointAtParameter(0.3).Y, 12);
            Assert.Equal(Parametrization2.GetDistanceAtParameter(loop, 0.3), loop.GetDistanceAtParameter(0.3), 12);
            Assert.Equal(Parametrization2.GetParameterAtDistance(loop, 260.0), loop.GetParameterAtDistance(260.0), 12);
            Assert.Equal(Parametrization2.GetParameterAtPoint(loop, point), loop.GetParameterAtPoint(point), 12);
            Assert.Equal(Parametrization2.GetDistanceAtPoint(loop, point), loop.GetDistanceAtPoint(point), 12);

            Assert.True(Projection2.ProjectToPolygonArc(loop, point).IsEqualTo(loop.GetClosestPointOnBoundary(point)));
            Assert.Equal(Containment2.Locate(loop, point), loop.Locate(point));
            Assert.Equal(Containment2.IsPointOn(loop, point), loop.IsPointOn(point));
            Assert.Equal(Containment2.Contains(loop, point), loop.Contains(point));

            GeoPolylineArc2 chain = Chain();

            Assert.True(Projection2.ProjectToPolylineArc(chain, point).IsEqualTo(chain.GetClosestPointOnBoundary(point)));
            Assert.Equal(Containment2.Locate(chain, point), chain.Locate(point));
            Assert.Equal(Parametrization2.GetDistanceAtPoint(chain, point), chain.GetDistanceAtPoint(point), 12);
        }

        [Fact]
        public void EveryToleranceOverloadIsThereAndAgreesWithTheGlobalOne()
        {
            GeoPolygonArc2 loop = Loop();
            GeoPolylineArc2 chain = Chain();
            Tolerance global = Tolerance.Global;
            var point = new GeoPoint2(230, 40);

            Assert.Equal(loop.DistanceTo(point), loop.DistanceTo(point, global), 12);
            Assert.Equal(loop.DistanceTo(Line()), loop.DistanceTo(Line(), global), 12);
            Assert.Equal(loop.DistanceTo(Arc()), loop.DistanceTo(Arc(), global), 12);
            Assert.Equal(loop.DistanceTo(Circle()), loop.DistanceTo(Circle(), global), 12);
            Assert.Equal(loop.GetIntersections(Line()).Length, loop.GetIntersections(Line(), global).Length);
            Assert.Equal(loop.CollidesWith(Line()), loop.CollidesWith(Line(), global));
            Assert.Equal(loop.Locate(point), loop.Locate(point, global));
            Assert.Equal(loop.Contains(point), loop.Contains(point, global));
            Assert.Equal(loop.IsPointOn(point), loop.IsPointOn(point, global));
            Assert.Equal(loop.GetParameterAtPoint(point), loop.GetParameterAtPoint(point, global), 12);
            Assert.Equal(loop.GetDistanceAtPoint(point), loop.GetDistanceAtPoint(point, global), 12);
            Assert.True(loop.GetClosestPointOnBoundary(point).IsEqualTo(loop.GetClosestPointOnBoundary(point, global)));

            Assert.Equal(chain.DistanceTo(point), chain.DistanceTo(point, global), 12);
            Assert.Equal(chain.DistanceTo(Circle()), chain.DistanceTo(Circle(), global), 12);
            Assert.Equal(chain.Locate(point), chain.Locate(point, global));

            // And the ones that reshape the chain.
            Assert.True(loop.Offset(10.0)[0].IsEqualTo(loop.Offset(10.0, global)[0]));
            Assert.True(loop.Offset(10.0, OffsetJoin.Round)[0].IsEqualTo(loop.Offset(10.0, OffsetJoin.Round, global)[0]));
            Assert.True(loop.Offset(10.0, OffsetOptions.Default)[0].IsEqualTo(loop.Offset(10.0, OffsetOptions.Default, global)[0]));
            Assert.True(chain.Offset(10.0)[0].IsEqualTo(chain.Offset(10.0, global)[0]));
            Assert.True(chain.Offset(10.0, OffsetJoin.Miter)[0].IsEqualTo(chain.Offset(10.0, OffsetJoin.Miter, global)[0]));
            Assert.True(chain.Offset(10.0, OffsetOptions.Default)[0].IsEqualTo(chain.Offset(10.0, OffsetOptions.Default, global)[0]));

            Assert.Equal(loop.SplitAtDistances(new[] { 100.0, 300.0 }).Length, loop.SplitAtDistances(new[] { 100.0, 300.0 }, global).Length);
            Assert.Equal(chain.SplitAtDistances(new[] { 100.0 }).Length, chain.SplitAtDistances(new[] { 100.0 }, global).Length);
        }

        [Fact]
        public void TheSplittingMirrorsReachTheSameAnswers()
        {
            GeoPolygonArc2 loop = Loop();
            GeoPolylineArc2 chain = Chain();

            Assert.Equal(
                Splition2.TrySplitBy(loop, Line(), out GeoPolylineArc2[] fromStatic),
                loop.TrySplitBy(Line(), out GeoPolylineArc2[] fromInstance));
            Assert.Equal(fromStatic.Length, fromInstance.Length);

            Assert.Equal(
                Splition2.TrySplitBy(loop, Plate(), out GeoPolylineArc2[] byPlate),
                loop.TrySplitBy(Plate(), out GeoPolylineArc2[] byPlateAgain));
            Assert.Equal(byPlate.Length, byPlateAgain.Length);

            Assert.Equal(
                Splition2.TrySplitAtDistance(chain, 100.0, out GeoPolylineArc2 a, out GeoPolylineArc2 b),
                chain.TrySplitAtDistance(100.0, out GeoPolylineArc2 c, out GeoPolylineArc2 d));
            Assert.True(a.IsEqualTo(c));
            Assert.True(b.IsEqualTo(d));

            Assert.Equal(
                Splition2.TrySplitBy(chain, chain.GetPointAtDistance(150.0), out _, out _),
                chain.TrySplitBy(chain.GetPointAtDistance(150.0), out _, out _));

            Assert.Equal(
                Splition2.TrySplitBy(chain, Loop(), out GeoPolylineArc2[] byLoop),
                chain.TrySplitBy(Loop(), out GeoPolylineArc2[] byLoopAgain));
            Assert.Equal(byLoop.Length, byLoopAgain.Length);

            Assert.Equal(
                Splition2.TrySplitBy(chain, new[] { chain.GetPointAtDistance(80.0) }, out GeoPolylineArc2[] byPoints),
                chain.TrySplitBy(new[] { chain.GetPointAtDistance(80.0) }, out GeoPolylineArc2[] byPointsAgain));
            Assert.Equal(byPoints.Length, byPointsAgain.Length);

            Assert.Equal(
                Splition2.TrySplitBy(chain, Arc(), out GeoPolylineArc2[] byArc),
                chain.TrySplitBy(Arc(), out GeoPolylineArc2[] byArcAgain));
            Assert.Equal(byArc.Length, byArcAgain.Length);

            Assert.Equal(
                Splition2.TrySplitBy(chain, Plate(), out GeoPolylineArc2[] byPlateOnChain),
                chain.TrySplitBy(Plate(), out GeoPolylineArc2[] byPlateOnChainAgain));
            Assert.Equal(byPlateOnChain.Length, byPlateOnChainAgain.Length);
        }
    }
}
