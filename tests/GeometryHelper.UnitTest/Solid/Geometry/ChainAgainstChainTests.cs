using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The last three pieces of the arc work: cutting a bar by a box or by a whole array of them, asking a bar
    /// about a segment or a ray, and asking one bar about another.
    /// </summary>
    /// <remarks>
    /// The chain-against-chain reading was held back on purpose because it walks every pair of edges rather than
    /// every edge. What makes it usable is that each edge carries a box round itself, so a pair whose boxes
    /// cannot reach each other is dropped before any arithmetic — and bars in a model are mostly far apart.
    /// </remarks>
    public class ChainAgainstChainTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A bar in z = 0: along x to (400, 0), then up to (400, 200), with a fifty radius at the bend.</summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

        /// <summary>A stirrup: a two hundred square loop in z = 0 with its corners rounded.</summary>
        private static GeoPolygonArc3 Stirrup() => new GeoPolygonArc3(new GeoPolygon3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0),
            new GeoPoint3(200, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(30.0);

        #region Cutting by a box and by several cutters

        [Fact]
        public void ABarCutByABoxKnowsWhatIsInTheOpeningAndWhatIsNot()
        {
            GeoPolylineArc3 bar = Bar();
            var opening = new GeoObb3(new GeoPoint3(200, 0, 0), 200, 200, 200);

            Assert.True(bar.TrySplitBy(opening, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));

            Assert.NotEmpty(inside);
            Assert.NotEmpty(outside);
            Assert.Equal(bar.Length, inside.Concat(outside).Sum(piece => piece.Length), 6);

            // The box is asked as itself and as the body it bounds, and the two agree.
            Assert.True(bar.TrySplitBy(opening.ToSolid(), out GeoPolylineArc3[] asBody, out _));
            Assert.Equal(inside.Sum(p => p.Length), asBody.Sum(p => p.Length), 6);

            // The square box agrees too.
            var square = new GeoAabb3(new GeoPoint3(100, -100, -100), new GeoPoint3(300, 100, 100));

            Assert.True(bar.TrySplitBy(square, out GeoPolylineArc3[] fromSquare, out _));
            Assert.Equal(200.0, fromSquare.Sum(p => p.Length), 6);
        }

        [Fact]
        public void ABarCutByEveryOpeningOnTheDrawingAtOnce()
        {
            GeoPolylineArc3 bar = Bar();

            // Three openings along the bar, the last one nowhere near it.
            var openings = new[]
            {
                new GeoAabb3(new GeoPoint3(50, -50, -50), new GeoPoint3(100, 50, 50)),
                new GeoAabb3(new GeoPoint3(200, -50, -50), new GeoPoint3(250, 50, 50)),
                new GeoAabb3(new GeoPoint3(5000, -50, -50), new GeoPoint3(5050, 50, 50)),
            };

            Assert.True(bar.TrySplitBy(openings, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));

            // Two openings bite, fifty each.
            Assert.Equal(2, inside.Length);
            Assert.Equal(100.0, inside.Sum(piece => piece.Length), 6);
            Assert.Equal(bar.Length, inside.Concat(outside).Sum(piece => piece.Length), 6);

            // A gap in the array is passed over rather than throwing, and the array itself may not be null.
            var withGaps = new[] { openings[0], GeoAabb3.Empty, openings[1] };

            Assert.True(bar.TrySplitBy(withGaps, out GeoPolylineArc3[] stillInside, out _));
            Assert.Equal(100.0, stillInside.Sum(piece => piece.Length), 6);

            Assert.Throws<ArgumentNullException>(() => bar.TrySplitBy((GeoAabb3[])null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => bar.TrySplitBy((GeoObb3[])null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => bar.TrySplitBy((GeoSolid3[])null, out _, out _));
        }

        [Fact]
        public void OverlappingCuttersBehaveAsTheOneRegionTheyCover()
        {
            GeoPolylineArc3 bar = Bar();

            var first = new GeoAabb3(new GeoPoint3(100, -50, -50), new GeoPoint3(200, 50, 50));
            var second = new GeoAabb3(new GeoPoint3(150, -50, -50), new GeoPoint3(250, 50, 50));

            Assert.True(bar.TrySplitBy(new[] { first, second }, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));

            // A hundred to two fifty is a hundred and fifty of bar. The cut is made at every cutter's surface,
            // the ones inside the union included, so it arrives as three pieces rather than one -- the boundary
            // between two overlapping openings is still a boundary.
            Assert.Equal(150.0, inside.Sum(piece => piece.Length), 6);
            Assert.Equal(bar.Length, inside.Concat(outside).Sum(piece => piece.Length), 6);

            // What matters is that none of it is counted twice, and that the straight version reads it the same
            // way: overlapping cutters behave as the one region they cover, not as two regions.
            var straight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0));

            Assert.True(straight.TrySplitBy(new[] { first, second }, out GeoPolyline3[] straightInside, out _));
            Assert.Equal(inside.Length, straightInside.Length);
            Assert.Equal(inside.Sum(p => p.Length), straightInside.Sum(p => p.Length), 6);
        }

        [Fact]
        public void AStirrupCanBeCutByABoxToo()
        {
            GeoPolygonArc3 stirrup = Stirrup();
            var box = new GeoObb3(new GeoPoint3(0, 100, 0), 200, 400, 200);

            Assert.True(stirrup.TrySplitBy(box, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));
            Assert.NotEmpty(inside);
            Assert.NotEmpty(outside);
            Assert.Equal(stirrup.Length, inside.Concat(outside).Sum(piece => piece.Length), 6);

            Assert.True(stirrup.TrySplitBy(new[] { box }, out GeoPolylineArc3[] fromArray, out _));
            Assert.Equal(inside.Sum(p => p.Length), fromArray.Sum(p => p.Length), 6);
        }

        [Fact]
        public void ABarCanBeCutIntoAScheduleOfLengthsAtOnce()
        {
            GeoPolylineArc3 bar = Bar();

            Assert.True(bar.SplitAtDistances(new[] { 100.0, 300.0, 5000.0 }, out GeoPolylineArc3[] pieces));

            Assert.Equal(3, pieces.Length);
            Assert.Equal(100.0, pieces[0].Length, 6);
            Assert.Equal(200.0, pieces[1].Length, 6);
            Assert.Equal(bar.Length, pieces.Sum(piece => piece.Length), 6);

            // Nothing in range cuts nothing, and the bar comes back whole.
            Assert.False(bar.SplitAtDistances(new[] { -5.0, 9999.0 }, out GeoPolylineArc3[] whole));
            Assert.Single(whole);
            Assert.Throws<ArgumentNullException>(() => bar.SplitAtDistances(null, out _));
        }

        #endregion

        #region A segment and a ray as probes

        [Fact]
        public void TwoSegmentsInSpaceCrossAsAListOfOne()
        {
            var first = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0));
            var crossing = new GeoLine3(new GeoPoint3(100, -50, 0), new GeoPoint3(100, 50, 0));

            Assert.Single(first.GetIntersections(crossing));
            Assert.True(first.GetIntersections(crossing)[0].IsEqualTo(new GeoPoint3(100, 0, 0), Loose));
            Assert.True(first.CollidesWith(crossing));

            // A segment keeps its single-point TryIntersectWith and gains no array twin: a second one differing
            // only in the shape of its out would make every existing call ambiguous.
            Assert.True(first.TryIntersectWith(crossing, out GeoPoint3 found));
            Assert.True(found.IsEqualTo(new GeoPoint3(100, 0, 0), Loose));

            // Skew segments pass each other and never meet.
            var skew = new GeoLine3(new GeoPoint3(100, -50, 50), new GeoPoint3(100, 50, 50));

            Assert.Empty(first.GetIntersections(skew));
            Assert.False(first.CollidesWith(skew));

            // Two collinear segments that overlap meet along a length, so no place is named and the collision
            // is what says they touch -- the reading two arcs of one circle get.
            var along = new GeoLine3(new GeoPoint3(100, 0, 0), new GeoPoint3(300, 0, 0));

            Assert.Empty(first.GetIntersections(along));
            Assert.True(first.CollidesWith(along));
        }

        [Fact]
        public void ASegmentAndARayMeetTheSameWayFromEitherSide()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0));
            var ray = new GeoRay3(new GeoPoint3(100, -50, 0), new GeoVector3(0, 1, 0));

            Assert.Single(line.GetIntersections(ray));
            Assert.True(line.GetIntersections(ray)[0].IsEqualTo(new GeoPoint3(100, 0, 0), Loose));
            Assert.True(line.CollidesWith(ray));

            // The other way round is the same question.
            Assert.Single(ray.GetIntersections(line));
            Assert.True(ray.CollidesWith(line));
            Assert.True(ray.GetIntersections(line)[0].IsEqualTo(line.GetIntersections(ray)[0], Loose));

            // A ray pointing away from the segment never reaches it.
            var away = new GeoRay3(new GeoPoint3(100, -50, 0), new GeoVector3(0, -1, 0));

            Assert.Empty(line.GetIntersections(away));
            Assert.False(line.CollidesWith(away));

            // A ray is a value type, so there is no null to guard against. One with no direction is its own
            // origin and nothing more, so the question becomes whether the segment holds that point.
            Assert.Single(line.GetIntersections(default(GeoRay3)));
            Assert.True(line.CollidesWith(default(GeoRay3)));
            Assert.Empty(line.Translate(new GeoVector3(0, 50, 0)).GetIntersections(default(GeoRay3)));
        }

        [Fact]
        public void ABarCanBeAskedAboutASegmentAndAboutARay()
        {
            GeoPolylineArc3 bar = Bar();

            // A segment crossing the first straight leg.
            var across = new GeoLine3(new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0));

            Assert.Single(bar.GetIntersections(across));
            Assert.True(bar.GetIntersections(across)[0].IsEqualTo(new GeoPoint3(200, 0, 0), Loose));
            Assert.True(bar.CollidesWith(across));
            Assert.True(bar.TryIntersectWith(across, out GeoPoint3[] found));
            Assert.Single(found);

            // A segment through the bend: the crossing is on the arc, at the bend's radius from its centre.
            var throughBend = new GeoLine3(new GeoPoint3(380, -50, 0), new GeoPoint3(380, 100, 0));
            GeoPoint3[] onBend = bar.GetIntersections(throughBend);

            Assert.Single(onBend);
            Assert.Equal(50.0, onBend[0].DistanceTo(new GeoPoint3(350, 50, 0)), 6);

            // A ray does the same, and one pointing away reaches nothing.
            var ray = new GeoRay3(new GeoPoint3(200, -50, 0), new GeoVector3(0, 1, 0));

            Assert.Single(bar.GetIntersections(ray));
            Assert.True(bar.CollidesWith(ray));
            Assert.Empty(bar.GetIntersections(new GeoRay3(new GeoPoint3(200, -50, 0), new GeoVector3(0, -1, 0))));

            // And a stirrup answers the same questions: a line across it crosses it twice.
            Assert.Equal(2, Stirrup().GetIntersections(new GeoLine3(new GeoPoint3(100, -50, 0), new GeoPoint3(100, 250, 0))).Length);
        }

        #endregion

        #region One chain against another

        [Fact]
        public void TwoBarsCrossingInOnePlaneFindEachOther()
        {
            GeoPolylineArc3 bar = Bar();

            // The same bar turned a quarter turn about the origin, so the two cross.
            GeoPolylineArc3 other = bar.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(200, 0, 0), GeoVector3.ZAxis, Math.PI / 2.0));

            GeoPoint3[] crossings = bar.GetIntersections(other);

            Assert.NotEmpty(crossings);
            Assert.True(bar.CollidesWith(other));
            Assert.True(bar.TryIntersectWith(other, out GeoPoint3[] found));
            Assert.Equal(crossings.Length, found.Length);

            // Every place is on both bars, which is the whole claim.
            foreach (GeoPoint3 crossing in crossings)
            {
                Assert.True(bar.IsPointOn(crossing, Loose), crossing.ToString());
                Assert.True(other.IsPointOn(crossing, Loose), crossing.ToString());
            }

            // Asked the other way round it is the same set.
            Assert.Equal(crossings.Length, other.GetIntersections(bar).Length);
        }

        [Fact]
        public void TwoBarsFarApartAreDroppedWithoutBeingSolved()
        {
            GeoPolylineArc3 bar = Bar();
            GeoPolylineArc3 away = bar.Translate(new GeoVector3(0, 0, 5000));

            Assert.Empty(bar.GetIntersections(away));
            Assert.False(bar.CollidesWith(away));
            Assert.False(bar.TryIntersectWith(away, out GeoPoint3[] none));
            Assert.Empty(none);

            // Skew and passing close by is still not touching.
            GeoPolylineArc3 justClear = bar.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(200, 0, 0), GeoVector3.ZAxis, Math.PI / 2.0))
                .Translate(new GeoVector3(0, 0, 10));

            Assert.Empty(bar.GetIntersections(justClear));
            Assert.False(bar.CollidesWith(justClear));
        }

        [Fact]
        public void ABarThroughAStirrupIsFoundFromEitherSide()
        {
            GeoPolygonArc3 stirrup = Stirrup();

            // A bar lying in the stirrup's plane and crossing two of its sides.
            var bar = new GeoPolylineArc3(new GeoPolyline3(
                new GeoPoint3(-100, 100, 0), new GeoPoint3(300, 100, 0)));

            GeoPoint3[] fromTheLoop = stirrup.GetIntersections(bar);

            Assert.Equal(2, fromTheLoop.Length);
            Assert.True(stirrup.CollidesWith(bar));

            Assert.Equal(2, bar.GetIntersections(stirrup).Length);
            Assert.True(bar.CollidesWith(stirrup));

            // The two readings name the same places.
            foreach (GeoPoint3 crossing in fromTheLoop)
            {
                Assert.Contains(bar.GetIntersections(stirrup), place => place.IsEqualTo(crossing, Loose));
            }
        }

        [Fact]
        public void AStraightChainIsJustAChainWithNoBendsInIt()
        {
            GeoPolylineArc3 bar = Bar();
            var straight = new GeoPolyline3(new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0));

            Assert.Single(bar.GetIntersections(straight));
            Assert.True(bar.CollidesWith(straight));

            // The straight chain answers the same question the other way round.
            Assert.Single(straight.GetIntersections(bar));
            Assert.True(straight.CollidesWith(bar));
            Assert.True(straight.GetIntersections(bar)[0].IsEqualTo(bar.GetIntersections(straight)[0], Loose));

            // And reading it as a curved chain gives the same answer, which is the point of the reading.
            Assert.Single(bar.GetIntersections(new GeoPolylineArc3(straight)));

            Assert.Throws<ArgumentNullException>(() => bar.GetIntersections((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => bar.GetIntersections((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => bar.CollidesWith((GeoPolygonArc3)null));
        }

        [Fact]
        public void TwoChainsSharingALengthTouchAndCrossNowhere()
        {
            var first = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0)));
            var along = new GeoPolylineArc3(new GeoPolyline3(new GeoPoint3(100, 0, 0), new GeoPoint3(300, 0, 0)));

            Assert.Empty(first.GetIntersections(along));
            Assert.True(first.CollidesWith(along));
        }

        #endregion

        #region The loop's joining line and nearest edge

        [Fact]
        public void AStirrupSaysWhereItComesNearestAPointAndWhichEdgeThatIs()
        {
            GeoPolygonArc3 stirrup = Stirrup();
            var outside = new GeoPoint3(-50, 100, 0);

            GeoLine3 join = stirrup.GetShortestLineTo(outside);

            // The line leaves the boundary and lands on the point, which is the direction the house keeps.
            Assert.True(join.EndPoint.IsEqualTo(outside, Loose));
            Assert.Equal(stirrup.DistanceTo(outside), join.Length, 6);
            Assert.True(stirrup.IsPointOn(join.StartPoint, Loose));

            GeoEdge3 near = stirrup.GetClosestEdge(outside);

            Assert.True(near.IsPointOn(join.StartPoint, Loose));

            // A point off the plane needs no coplanarity: it stands the same height above every edge, so the
            // nearest place is the nearest place to its shadow.
            var above = new GeoPoint3(-50, 100, 300);

            Assert.Equal(stirrup.DistanceTo(above), stirrup.GetShortestLineTo(above).Length, 6);
            Assert.True(stirrup.GetClosestEdge(above).IsEqualTo(near, Loose));
        }

        [Fact]
        public void AStirrupMeasuresToAnotherShapeInItsPlaneAndRefusesOneOutsideIt()
        {
            GeoPolygonArc3 stirrup = Stirrup();

            var line = new GeoLine3(new GeoPoint3(-100, 100, 0), new GeoPoint3(-50, 100, 0));

            Assert.Equal(50.0, stirrup.GetShortestLineTo(line).Length, 6);
            Assert.True(stirrup.GetClosestEdge(line).IsPointOn(stirrup.GetShortestLineTo(line).StartPoint, Loose));

            // A polygon, a loop, a chain and a face in the same plane all answer.
            var plate = new GeoPolygon3(
                new GeoPoint3(-200, 0, 0), new GeoPoint3(-100, 0, 0),
                new GeoPoint3(-100, 200, 0), new GeoPoint3(-200, 200, 0));

            Assert.Equal(100.0, stirrup.GetShortestLineTo(plate).Length, 6);
            Assert.Equal(100.0, stirrup.GetShortestLineTo(new GeoFace3(plate)).Length, 6);
            Assert.Equal(100.0, stirrup.GetShortestLineTo(new GeoPolygonArc3(plate)).Length, 6);
            Assert.Equal(100.0, stirrup.GetShortestLineTo(new GeoPolyline3(
                new GeoPoint3(-100, 0, 0), new GeoPoint3(-100, 200, 0))).Length, 6);

            // Anything out of the plane is refused rather than quietly projected in.
            GeoPolygonArc3 above = stirrup.Translate(new GeoVector3(0, 0, 100));

            Assert.Throws<ArgumentException>(() => stirrup.GetShortestLineTo(above));
            Assert.Throws<ArgumentException>(() => stirrup.GetShortestLineTo(plate.Translate(new GeoVector3(0, 0, 100))));
            Assert.Throws<ArgumentException>(() => stirrup.GetShortestLineTo(line.Translate(new GeoVector3(0, 0, 100))));
            Assert.Throws<ArgumentException>(() => stirrup.GetClosestEdge(line.Translate(new GeoVector3(0, 0, 100))));

            Assert.Throws<ArgumentNullException>(() => stirrup.GetShortestLineTo((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => stirrup.GetShortestLineTo((GeoFace3)null));
        }

        [Fact]
        public void AnArcAndACircleInTheStirrupsPlaneAreMeasuredToo()
        {
            GeoPolygonArc3 stirrup = Stirrup();

            // A circle well clear of the stirrup, in the same plane.
            var circle = new GeoCircle3(new GeoPoint3(-200, 100, 0), GeoVector3.ZAxis, 50.0);

            Assert.Equal(150.0, stirrup.GetShortestLineTo(circle).Length, 6);
            Assert.True(stirrup.GetClosestEdge(circle).IsPointOn(stirrup.GetShortestLineTo(circle).StartPoint, Loose));

            GeoArc3 arc = GeoArc3.FromThreePoints(
                new GeoPoint3(-250, 100, 0), new GeoPoint3(-200, 150, 0), new GeoPoint3(-150, 100, 0));

            Assert.Equal(150.0, stirrup.GetShortestLineTo(arc).Length, 6);

            var edge = new GeoEdge3(new GeoPoint3(-150, 0, 0), new GeoPoint3(-150, 200, 0));

            Assert.Equal(150.0, stirrup.GetShortestLineTo(edge).Length, 6);
            Assert.NotNull(stirrup.GetClosestEdge(edge).ToString());

            // Standing the circle up takes it out of the plane, and it is refused.
            Assert.Throws<ArgumentException>(() => stirrup.GetShortestLineTo(
                new GeoCircle3(new GeoPoint3(-200, 100, 0), GeoVector3.XAxis, 50.0)));
        }

        #endregion

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolylineArc3 bar = Bar();
            GeoPolygonArc3 stirrup = Stirrup();
            var box = new GeoObb3(new GeoPoint3(200, 0, 0), 200, 200, 200);
            var line = new GeoLine3(new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0));
            var ray = new GeoRay3(new GeoPoint3(200, -50, 0), new GeoVector3(0, 1, 0));
            var point = new GeoPoint3(-50, 100, 0);

            Assert.Equal(
                bar.TrySplitBy(box, out GeoPolylineArc3[] a, out _),
                bar.TrySplitBy(box, out GeoPolylineArc3[] b, out _, global));
            Assert.Equal(a.Length, b.Length);

            Assert.Equal(
                bar.TrySplitBy(new[] { box }, out GeoPolylineArc3[] c, out _),
                bar.TrySplitBy(new[] { box }, out GeoPolylineArc3[] d, out _, global));
            Assert.Equal(c.Length, d.Length);

            Assert.Equal(bar.GetIntersections(line).Length, bar.GetIntersections(line, global).Length);
            Assert.Equal(bar.GetIntersections(ray).Length, bar.GetIntersections(ray, global).Length);
            Assert.Equal(bar.CollidesWith(line), bar.CollidesWith(line, global));
            Assert.Equal(bar.CollidesWith(ray), bar.CollidesWith(ray, global));
            Assert.Equal(stirrup.GetIntersections(bar).Length, stirrup.GetIntersections(bar, global).Length);
            Assert.Equal(stirrup.CollidesWith(bar), stirrup.CollidesWith(bar, global));
            Assert.Equal(stirrup.GetShortestLineTo(point).Length, stirrup.GetShortestLineTo(point, global).Length, 9);
            Assert.Equal(stirrup.GetClosestEdge(point).Length, stirrup.GetClosestEdge(point, global).Length, 9);

            Assert.Throws<ArgumentNullException>(() => ArcChain3.GetIntersections((GeoPolylineArc3)null, line));
            Assert.Throws<ArgumentNullException>(() => ArcChain3.GetIntersections(bar, (GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => ArcChain3.SplitAtDistances((GeoPolylineArc3)null, new[] { 1.0 }, out _));
        }
    }
}
