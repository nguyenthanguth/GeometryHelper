using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// Extending and trimming the curved shapes. Only <see cref="GeoLine2"/> and <see cref="GeoLine3"/> could be
    /// lengthened or cut back, which left the one thing a reinforcing schedule asks about every day — pulling a
    /// bar's end leg out for anchorage — with no answer but rebuilding the chain by hand.
    /// </summary>
    /// <remarks>
    /// Two rules the tests hold to. An arc is lengthened <b>along itself</b>: the centre and the radius stay and
    /// the sweep grows, so the distance asked for is arc length and the curve does not move. A chain is
    /// lengthened by its <b>end leg</b> only, and a curved end leg carries on round rather than flying off
    /// straight.
    /// </remarks>
    public class LengthenCurvedTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A quarter of a hundred circle in the plane, counter-clockwise.</summary>
        private static GeoArc2 Quarter() => new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI / 2.0);

        /// <summary>The same quarter in space, built from three points so no angle convention is assumed.</summary>
        private static GeoArc3 Quarter3() => GeoArc3.FromThreePoints(
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100.0 * Math.Cos(Math.PI / 4.0), 100.0 * Math.Sin(Math.PI / 4.0), 0),
            new GeoPoint3(0, 100, 0));

        /// <summary>A bar in z = 0 with a fifty radius at its one bend.</summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

        #region Arcs

        [Fact]
        public void AnArcIsLengthenedAlongItselfAndKeepsItsCentreAndRadius()
        {
            GeoArc2 quarter = Quarter();
            double length = quarter.Length;

            GeoArc2 longer = quarter.Extend(50.0, LineEnd.End);

            Assert.Equal(length + 50.0, longer.Length, 6);
            Assert.Equal(quarter.Radius, longer.Radius, 9);
            Assert.True(longer.Center.IsEqualTo(quarter.Center, Loose));

            // The end moved and the start did not.
            Assert.True(longer.StartPoint.IsEqualTo(quarter.StartPoint, Loose));
            Assert.False(longer.EndPoint.IsEqualTo(quarter.EndPoint, Loose));

            // The other end moves the other way, and the end stays put.
            GeoArc2 backwards = quarter.Extend(50.0, LineEnd.Start);

            Assert.Equal(length + 50.0, backwards.Length, 6);
            Assert.True(backwards.EndPoint.IsEqualTo(quarter.EndPoint, Loose));
            Assert.False(backwards.StartPoint.IsEqualTo(quarter.StartPoint, Loose));

            // A negative distance takes length away.
            Assert.Equal(length - 50.0, quarter.Extend(-50.0, LineEnd.End).Length, 6);

            // Both ends at once.
            Assert.Equal(length + 80.0, quarter.Extend(30.0, 50.0).Length, 6);
        }

        [Fact]
        public void AClockwiseArcLengthensTheWayItTravels()
        {
            // The same quarter drawn the other way round.
            var clockwise = new GeoArc2(new GeoPoint2(0, 0), 100.0, Math.PI / 2.0, 0.0, true);

            Assert.True(clockwise.IsClockwise);

            GeoArc2 longer = clockwise.Extend(50.0, LineEnd.End);

            Assert.Equal(clockwise.Length + 50.0, longer.Length, 6);
            Assert.True(longer.IsClockwise);
            Assert.True(longer.StartPoint.IsEqualTo(clockwise.StartPoint, Loose));
        }

        [Fact]
        public void AnArcCannotBeCarriedPastAWholeTurnOrShortenedAwayToNothing()
        {
            GeoArc2 quarter = Quarter();
            double wholeCircle = 2.0 * Math.PI * 100.0;

            // A quarter is a quarter of the way round; there is three quarters of the circle left to take.
            Assert.Equal(wholeCircle * 0.9, quarter.ExtendToLength(wholeCircle * 0.9, LineEnd.End).Length, 6);

            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.Extend(wholeCircle, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.ExtendToLength(wholeCircle, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.Extend(-quarter.Length, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.ExtendToLength(0.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.Extend(double.NaN, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => quarter.Extend(50.0, (LineEnd)99));
        }

        [Fact]
        public void AnArcReachesAPointOnItsOwnCircleAndStopsAtAPointOnItself()
        {
            GeoArc2 quarter = Quarter();

            // Three quarters of the way round, which the quarter does not reach yet.
            var further = new GeoPoint2(0, -100);

            Assert.True(quarter.TryExtendTo(further, LineEnd.End, out GeoArc2 longer));
            Assert.Equal(2.0 * Math.PI * 100.0 * 0.75, longer.Length, 6);
            Assert.True(longer.EndPoint.IsEqualTo(further, Loose));

            // A point the arc already covers is not reached by extending it: that is trimming.
            GeoPoint2 inside = quarter.GetPointAtParameter(0.5);

            Assert.False(quarter.TryExtendTo(inside, LineEnd.End, out _));
            Assert.True(quarter.TryTrimTo(inside, LineEnd.End, out GeoArc2 shorter));
            Assert.Equal(quarter.Length / 2.0, shorter.Length, 6);
            Assert.True(shorter.StartPoint.IsEqualTo(quarter.StartPoint, Loose));
            Assert.True(shorter.EndPoint.IsEqualTo(inside, Loose));

            // Trimming from the start keeps the end.
            Assert.True(quarter.TryTrimTo(inside, LineEnd.Start, out GeoArc2 fromStart));
            Assert.True(fromStart.EndPoint.IsEqualTo(quarter.EndPoint, Loose));
            Assert.Equal(quarter.Length / 2.0, fromStart.Length, 6);

            // A point off the circle is refused rather than drawn in to the nearest place on it.
            Assert.False(quarter.TryExtendTo(new GeoPoint2(0, -50), LineEnd.End, out _));
            Assert.False(quarter.TryTrimTo(new GeoPoint2(50, 50), LineEnd.End, out _));
        }

        [Fact]
        public void AnArcInSpaceAnswersTheSameWayAsOneInThePlane()
        {
            GeoArc3 quarter = Quarter3();

            Assert.Equal(2.0 * Math.PI * 100.0 / 4.0, quarter.Length, 6);

            GeoArc3 longer = quarter.Extend(50.0, LineEnd.End);

            Assert.Equal(quarter.Length + 50.0, longer.Length, 6);
            Assert.Equal(100.0, longer.Radius, 6);
            Assert.True(longer.Center.IsEqualTo(quarter.Center, Loose));
            Assert.True(longer.StartPoint.IsEqualTo(quarter.StartPoint, Loose));

            // It stays in its own plane.
            GeoPlane3 plane = quarter.GetPlane();

            Assert.True(plane.IsPointOn(longer.EndPoint, Loose));

            // And a point on its circle is reached in space too.
            var further = new GeoPoint3(0, -100, 0);

            Assert.True(quarter.TryExtendTo(further, LineEnd.End, out GeoArc3 reached));
            Assert.True(reached.EndPoint.IsEqualTo(further, Loose));

            // A point the same distance from the centre but off the plane is on a sphere, not on this circle.
            Assert.False(quarter.TryExtendTo(new GeoPoint3(0, 0, 100), LineEnd.End, out _));

            GeoPoint3 inside = quarter.GetPointAtParameter(0.5);

            Assert.True(quarter.TryTrimTo(inside, LineEnd.End, out GeoArc3 shorter));
            Assert.Equal(quarter.Length / 2.0, shorter.Length, 6);
        }

        [Fact]
        public void ATiltedArcIsLengthenedInItsOwnPlane()
        {
            GeoArc3 quarter = Quarter3();
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.XAxis, Math.PI / 3.0);
            GeoArc3 tilted = quarter.TransformBy(turn);

            GeoArc3 longer = tilted.Extend(50.0, LineEnd.End);

            Assert.Equal(tilted.Length + 50.0, longer.Length, 6);
            Assert.True(tilted.GetPlane().IsPointOn(longer.EndPoint, Loose));

            // The answer is the flat one turned the same way, not something re-fitted.
            Assert.True(longer.EndPoint.IsEqualTo(quarter.Extend(50.0, LineEnd.End).EndPoint.TransformBy(turn), Loose));
        }

        #endregion

        #region Chains

        [Fact]
        public void ABarsEndLegIsPulledOutForAnchorageAndTheBendsSurvive()
        {
            GeoPolylineArc3 bar = Bar();
            double length = bar.Length;

            GeoPolylineArc3 longer = bar.Extend(300.0, LineEnd.End);

            Assert.Equal(length + 300.0, longer.Length, 6);

            // The bend is untouched: same count, same radius, same centre.
            Assert.Single(longer.GetEdges(), edge => edge.IsArc);
            Assert.Equal(50.0, longer.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.True(longer.StartPoint.IsEqualTo(bar.StartPoint, Loose));

            // The end carried on along its own leg, which runs in +y.
            Assert.True(longer.EndPoint.IsEqualTo(bar.EndPoint.Add(new GeoVector3(0, 300, 0)), Loose));

            // The other end carries on along the first leg, which runs in -x from the start.
            GeoPolylineArc3 fromStart = bar.Extend(300.0, LineEnd.Start);

            Assert.Equal(length + 300.0, fromStart.Length, 6);
            Assert.True(fromStart.EndPoint.IsEqualTo(bar.EndPoint, Loose));
            Assert.True(fromStart.StartPoint.IsEqualTo(bar.StartPoint.Add(new GeoVector3(-300, 0, 0)), Loose));
        }

        [Fact]
        public void ACurvedEndLegCarriesOnRoundRatherThanFlyingOffStraight()
        {
            // A bar whose last leg is the bend itself: the whole straight tail after the fillet is cut off, so
            // the cut lands on the arc's own end and the break falls between edges.
            GeoPolylineArc3 bar = Bar();

            Assert.True(bar.TrySplitBy(bar.GetPointAtDistance(bar.Length - 150.0), out GeoPolylineArc3[] pieces));

            GeoPolylineArc3 endsInAnArc = pieces[0];

            Assert.True(endsInAnArc.GetEdges().Last().IsArc);

            GeoPolylineArc3 longer = endsInAnArc.Extend(20.0, LineEnd.End);

            Assert.Equal(endsInAnArc.Length + 20.0, longer.Length, 6);

            // The last edge is still an arc of the same radius about the same centre: it carried on round.
            GeoEdge3 grown = longer.GetEdges().Last();

            Assert.True(grown.IsArc);
            Assert.Equal(50.0, grown.ToArc().Radius, 6);
            Assert.True(grown.ToArc().Center.IsEqualTo(endsInAnArc.GetEdges().Last().ToArc().Center, Loose));
        }

        [Fact]
        public void AChainIsCarriedToATotalLengthAndOutwardsOnly()
        {
            GeoPolylineArc3 bar = Bar();

            GeoPolylineArc3 toLength = bar.ExtendToLength(2000.0, LineEnd.End);

            Assert.Equal(2000.0, toLength.Length, 6);

            // Shortening belongs to the cutting family, which keeps the bends, so this refuses it outright.
            Assert.Throws<ArgumentOutOfRangeException>(() => bar.Extend(-100.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => bar.Extend(0.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => bar.ExtendToLength(bar.Length / 2.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => bar.Extend(50.0, (LineEnd)99));
        }

        [Fact]
        public void AChainIsCutBackToAPointOnItAndKeepsTheOtherEnd()
        {
            GeoPolylineArc3 bar = Bar();
            GeoPoint3 stop = bar.GetPointAtDistance(200.0);

            Assert.True(bar.TryTrimTo(stop, LineEnd.End, out GeoPolylineArc3 fromStart));
            Assert.Equal(200.0, fromStart.Length, 6);
            Assert.True(fromStart.StartPoint.IsEqualTo(bar.StartPoint, Loose));

            Assert.True(bar.TryTrimTo(stop, LineEnd.Start, out GeoPolylineArc3 fromEnd));
            Assert.Equal(bar.Length - 200.0, fromEnd.Length, 6);
            Assert.True(fromEnd.EndPoint.IsEqualTo(bar.EndPoint, Loose));

            // The two pieces add back up to the bar, and the one holding the bend still holds it.
            Assert.Equal(bar.Length, fromStart.Length + fromEnd.Length, 6);
            Assert.Contains(true, fromEnd.GetEdges().Select(edge => edge.IsArc));

            // A point that is not on the bar cuts nothing.
            Assert.False(bar.TryTrimTo(new GeoPoint3(0, 5000, 0), LineEnd.End, out GeoPolylineArc3 whole));
            Assert.Equal(bar.Length, whole.Length, 6);
        }

        [Fact]
        public void ACutInsideABendLeavesAnArcOfTheSameRadius()
        {
            GeoPolylineArc3 bar = Bar();

            // The fillet is centred at (350, 50, 0) with radius 50, so this distance falls inside it.
            GeoPoint3 inBend = bar.GetPointAtDistance(350.0 + Math.PI * 50.0 / 4.0);

            Assert.True(bar.TryTrimTo(inBend, LineEnd.End, out GeoPolylineArc3 cut));
            Assert.Contains(true, cut.GetEdges().Select(edge => edge.IsArc));
            Assert.All(cut.GetEdges().Where(edge => edge.IsArc),
                edge => Assert.Equal(50.0, edge.ToArc().Radius, 6));
        }

        [Fact]
        public void TheStraightChainsAndTheCurvedOnesAgreeWhereThereIsNoArc()
        {
            var setOut = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));
            var asChain = new GeoPolylineArc3(setOut);

            Assert.Equal(setOut.Extend(300.0, LineEnd.End).Length, asChain.Extend(300.0, LineEnd.End).Length, 6);
            Assert.True(setOut.Extend(300.0, LineEnd.End).EndPoint.IsEqualTo(asChain.Extend(300.0, LineEnd.End).EndPoint, Loose));
            Assert.Equal(setOut.ExtendToLength(2000.0, LineEnd.Start).Length, asChain.ExtendToLength(2000.0, LineEnd.Start).Length, 6);

            // And the plane and space agree on the same chain laid flat.
            var flat = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 200));

            Assert.Equal(flat.Extend(300.0, LineEnd.End).Length, setOut.Extend(300.0, LineEnd.End).Length, 6);
            Assert.Equal(
                new GeoPolylineArc2(flat).Extend(300.0, LineEnd.End).Length,
                asChain.Extend(300.0, LineEnd.End).Length, 6);
        }

        #endregion

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoArc2 quarter = Quarter();
            GeoArc3 quarter3 = Quarter3();
            GeoPolylineArc3 bar = Bar();
            var flat = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 200));

            Assert.Equal(quarter.Extend(50.0, LineEnd.End).Length, quarter.Extend(50.0, LineEnd.End, global).Length, 9);
            Assert.Equal(quarter.Extend(10.0, 20.0).Length, quarter.Extend(10.0, 20.0, global).Length, 9);
            Assert.Equal(quarter.ExtendToLength(200.0, LineEnd.End).Length, quarter.ExtendToLength(200.0, LineEnd.End, global).Length, 9);
            Assert.Equal(quarter3.Extend(50.0, LineEnd.End).Length, quarter3.Extend(50.0, LineEnd.End, global).Length, 9);
            Assert.Equal(bar.Extend(50.0, LineEnd.End).Length, bar.Extend(50.0, LineEnd.End, global).Length, 9);
            Assert.Equal(flat.Extend(50.0, LineEnd.End).Length, flat.Extend(50.0, LineEnd.End, global).Length, 9);

            GeoPoint2 on = quarter.GetPointAtParameter(0.5);

            Assert.Equal(
                quarter.TryTrimTo(on, LineEnd.End, out GeoArc2 a),
                quarter.TryTrimTo(on, LineEnd.End, out GeoArc2 b, global));
            Assert.Equal(a.Length, b.Length, 9);

            Assert.Equal(
                bar.TryTrimTo(bar.GetPointAtDistance(200.0), LineEnd.End, out GeoPolylineArc3 c),
                bar.TryTrimTo(bar.GetPointAtDistance(200.0), LineEnd.End, out GeoPolylineArc3 d, global));
            Assert.Equal(c.Length, d.Length, 9);

            Assert.Throws<ArgumentNullException>(() => Lengthen2.Extend((GeoPolyline2)null, 10.0, LineEnd.End));
            Assert.Throws<ArgumentNullException>(() => Lengthen2.Extend((GeoPolylineArc2)null, 10.0, LineEnd.End));
            Assert.Throws<ArgumentNullException>(() => Lengthen3.Extend((GeoPolyline3)null, 10.0, LineEnd.End));
            Assert.Throws<ArgumentNullException>(() => Lengthen3.Extend((GeoPolylineArc3)null, 10.0, LineEnd.End));
            Assert.Throws<ArgumentNullException>(() => Lengthen3.TryTrimTo((GeoPolylineArc3)null, GeoPoint3.Origin, LineEnd.End, out _));
        }
    }
}
