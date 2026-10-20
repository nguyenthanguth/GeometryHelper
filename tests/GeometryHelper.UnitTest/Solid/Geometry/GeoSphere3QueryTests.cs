using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ball in space asked how far it is from every other shape, whether it touches it, where its surface is crossed and where
    /// the shortest segment between them runs, and the shape asked the same of the ball. Every expectation is worked out by hand, on
    /// the ball of radius 50 about the origin unless a test says otherwise.
    /// </summary>
    /// <remarks>
    /// The tolerances are a thousandth for points and 1E-5 for vectors, as the default. Offsets of 0.0005 and 0.002 sit either side of
    /// the thousandth, a factor of two clear of it. Asked from the other side, the answer is the same to the bit, and the segment is
    /// the same turned round.
    /// </remarks>
    public class GeoSphere3QueryTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z);

        private static GeoSphere3 Ball() => new GeoSphere3(P(0, 0, 0), 50);

        private static void AssertNear(GeoPoint3 expected, GeoPoint3 actual, double within = 1E-9)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertPoints(GeoPoint3[] expected, GeoPoint3[] actual, double within = 1E-9)
        {
            Assert.True(expected.Length == actual.Length, actual.Length + " points against " + expected.Length + ": " + string.Join(" ", actual));

            for (int i = 0; i < expected.Length; i++)
            {
                AssertNear(expected[i], actual[i], within);
            }
        }

        private static void AssertSegment(GeoPoint3 from, GeoPoint3 to, GeoLine3 actual, double within = 1E-9)
        {
            AssertNear(from, actual.StartPoint, within);
            AssertNear(to, actual.EndPoint, within);
        }

        private static void AssertTurnedRound(GeoLine3 forward, GeoLine3 back)
        {
            Assert.Equal(forward.StartPoint, back.EndPoint);
            Assert.Equal(forward.EndPoint, back.StartPoint);
        }

        private static GeoPolygon3 Floor(double x0, double y0, double x1, double y1, double z = 0)
            => new GeoPolygon3(P(x0, y0, z), P(x1, y0, z), P(x1, y1, z), P(x0, y1, z));

        #region A point

        [Fact]
        public void APointTwoHundredOut_IsOneHundredFiftyFromTheBall_AndOneInsideIsNought()
        {
            Assert.Equal(150.0, Ball().DistanceTo(P(200, 0, 0)), 12);
            Assert.Equal(0.0, Ball().DistanceTo(P(30, 0, 0)));
            Assert.Equal(0.0, Ball().DistanceTo(P(0, 0, 0)));
            Assert.Equal(0.0, Ball().DistanceTo(P(0, 50, 0)));
        }

        [Fact]
        public void APointAsksTheSameDistanceFromTheOtherSide_ToTheBit()
        {
            Assert.Equal(Ball().DistanceTo(P(120, 30, -7)), P(120, 30, -7).DistanceTo(Ball()));
        }

        [Fact]
        public void TheShortestLineToAPoint_LeavesTheSurfaceAndLandsOnIt()
        {
            AssertSegment(P(50, 0, 0), P(200, 0, 0), Ball().GetShortestLineTo(P(200, 0, 0), Strict));
            AssertSegment(P(0, 0, 50), P(0, 0, 80), Ball().GetShortestLineTo(P(0, 0, 80), Strict));
            Assert.Equal(30.0, Ball().GetShortestLineTo(P(0, 0, 80), Strict).Length, 9);
        }

        [Fact]
        public void TheShortestLineToAPointInsideTheBall_HasNoLengthAndLiesAtThePoint()
        {
            GeoLine3 line = Ball().GetShortestLineTo(P(30, 0, 0), Strict);

            Assert.Equal(P(30, 0, 0), line.StartPoint);
            Assert.Equal(P(30, 0, 0), line.EndPoint);
        }

        [Fact]
        public void TheShortestLineFromAPoint_IsTheOneToItTurnedRound()
        {
            AssertTurnedRound(Ball().GetShortestLineTo(P(200, 40, 0), Strict), P(200, 40, 0).GetShortestLineTo(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(P(200, 40, 0)), P(200, 40, 0).GetShortestLineTo(Ball()));
        }

        #endregion

        #region A segment

        [Fact]
        public void ASegmentAHundredOut_IsFiftyFromTheBall_AndOneThroughItIsNought()
        {
            var beside = new GeoLine3(P(100, -100, 0), P(100, 100, 0));
            var through = new GeoLine3(P(0, -100, 0), P(0, 100, 0));

            Assert.Equal(50.0, Ball().DistanceTo(beside, Strict), 9);
            Assert.Equal(0.0, Ball().DistanceTo(through, Strict));
            Assert.False(Ball().CollidesWith(beside, Strict));
            Assert.True(Ball().CollidesWith(through, Strict));
        }

        [Fact]
        public void ASegmentThroughTheBall_CrossesTheSurfaceTwice_InOrderAlongIt()
        {
            var through = new GeoLine3(P(0, -100, 0), P(0, 100, 0));

            AssertPoints(new[] { P(0, -50, 0), P(0, 50, 0) }, Ball().GetIntersections(through, Strict));
            AssertPoints(new[] { P(0, 50, 0), P(0, -50, 0) }, Ball().GetIntersections(through.Reverse(), Strict));
        }

        [Fact]
        public void ASegmentWhollyInside_CollidesButCrossesNowhere()
        {
            var inside = new GeoLine3(P(-10, 0, 0), P(10, 0, 0));

            Assert.True(Ball().CollidesWith(inside, Strict));
            Assert.Empty(Ball().GetIntersections(inside, Strict));
            Assert.False(Ball().TryIntersectWith(inside, out GeoPoint3[] found, Strict));
            Assert.Empty(found);
        }

        [Fact]
        public void ASegmentThatStartsInside_CrossesTheSurfaceOnce()
        {
            AssertPoints(new[] { P(50, 0, 0) }, Ball().GetIntersections(new GeoLine3(P(10, 0, 0), P(100, 0, 0)), Strict));
        }

        [Fact]
        public void ASegmentThatStopsAHalfThousandthShortOfTheSurface_TouchesItAtItsEnd_AndOneTwoThousandthsShortDoesNot()
        {
            var near = new GeoLine3(P(100, 0, 0), P(50.0005, 0, 0));
            var far = new GeoLine3(P(100, 0, 0), P(50.002, 0, 0));

            Assert.True(Ball().CollidesWith(near, Strict));
            AssertPoints(new[] { P(50.0005, 0, 0) }, Ball().GetIntersections(near, Strict), 1E-12);
            Assert.False(Ball().CollidesWith(far, Strict));
            Assert.Empty(Ball().GetIntersections(far, Strict));
        }

        [Fact]
        public void ASegmentNoLongerThanTheTolerance_IsItsStartPoint()
        {
            var dot = new GeoLine3(P(50, 0, 0), P(50.0001, 0, 0));
            var away = new GeoLine3(P(60, 0, 0), P(60.0001, 0, 0));

            AssertPoints(new[] { P(50, 0, 0) }, Ball().GetIntersections(dot, Strict), 1E-12);
            Assert.Empty(Ball().GetIntersections(away, Strict));
        }

        [Fact]
        public void TheShortestLineToASegment_LandsOnTheSegmentNearestTheCentre()
        {
            var beside = new GeoLine3(P(100, -100, 0), P(100, 100, 0));

            AssertSegment(P(50, 0, 0), P(100, 0, 0), Ball().GetShortestLineTo(beside, Strict));
            // A segment that ends beside the ball is nearest the centre at its end.
            AssertSegment(
                P(50 / Math.Sqrt(2.0), 50 / Math.Sqrt(2.0), 0),
                P(100, 100, 0),
                Ball().GetShortestLineTo(new GeoLine3(P(100, 100, 0), P(100, 200, 0)), Strict));
        }

        [Fact]
        public void TheShortestLineToASegmentThroughTheBall_HasNoLength()
        {
            GeoLine3 line = Ball().GetShortestLineTo(new GeoLine3(P(0, -100, 0), P(0, 100, 0)), Strict);

            Assert.Equal(0.0, line.Length);
            AssertNear(P(0, 0, 0), line.StartPoint);
        }

        [Fact]
        public void ASegmentAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var line = new GeoLine3(P(80, 70, -20), P(-30, 90, 60));

            Assert.Equal(Ball().DistanceTo(line, Strict), line.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().DistanceTo(line), line.DistanceTo(Ball()));
            Assert.Equal(Ball().CollidesWith(line, Strict), line.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(line), line.CollidesWith(Ball()));
            AssertTurnedRound(Ball().GetShortestLineTo(line, Strict), line.GetShortestLineTo(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(line), line.GetShortestLineTo(Ball()));

            var through = new GeoLine3(P(0, -100, 0), P(0, 100, 0));

            Assert.Equal(Ball().GetIntersections(through, Strict), through.GetIntersections(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(through), through.GetIntersections(Ball()));
            Assert.True(through.TryIntersectWith(Ball(), out GeoPoint3[] back, Strict));
            Assert.Equal(Ball().GetIntersections(through, Strict), back);
            Assert.True(through.TryIntersectWith(Ball(), out back));
            Assert.Equal(2, back.Length);
        }

        #endregion

        #region A ray

        [Fact]
        public void ARayRunningAwayFromTheBall_IsFiftyFromIt_AndOneRunningThroughItIsNought()
        {
            var away = new GeoRay3(P(100, 0, 0), V(1, 0, 0));
            var toward = new GeoRay3(P(100, 0, 0), V(-1, 0, 0));

            Assert.Equal(50.0, Ball().DistanceTo(away), 9);
            Assert.Equal(0.0, Ball().DistanceTo(toward));
            Assert.False(Ball().CollidesWith(away, Strict));
            Assert.True(Ball().CollidesWith(toward, Strict));
        }

        [Fact]
        public void ARayThroughTheBall_CrossesTheSurfaceTwice_AndOneThatStartsInsideOnce()
        {
            AssertPoints(new[] { P(50, 0, 0), P(-50, 0, 0) }, Ball().GetIntersections(new GeoRay3(P(100, 0, 0), V(-1, 0, 0)), Strict));
            AssertPoints(new[] { P(50, 0, 0) }, Ball().GetIntersections(new GeoRay3(P(10, 0, 0), V(1, 0, 0)), Strict));
            Assert.Empty(Ball().GetIntersections(new GeoRay3(P(100, 0, 0), V(1, 0, 0)), Strict));
        }

        [Fact]
        public void TheShortestLineToARay_LandsOnTheRayNearestTheCentre()
        {
            AssertSegment(P(50, 0, 0), P(100, 0, 0), Ball().GetShortestLineTo(new GeoRay3(P(100, 0, 0), V(1, 0, 0)), Strict));
            AssertSegment(P(0, 50, 0), P(0, 80, 0), Ball().GetShortestLineTo(new GeoRay3(P(-200, 80, 0), V(1, 0, 0)), Strict));
        }

        [Fact]
        public void ARayAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var ray = new GeoRay3(P(100, 20, 0), V(-1, 0.1, 0.05));

            Assert.Equal(Ball().DistanceTo(ray), ray.DistanceTo(Ball()));
            Assert.Equal(Ball().CollidesWith(ray, Strict), ray.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(ray, Strict), ray.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(ray, Strict), ray.GetShortestLineTo(Ball(), Strict));
        }

        #endregion

        #region An infinite line

        [Fact]
        public void ALineEightyOutAlongX_IsThirtyFromTheBall_WhicheverWayItRuns()
        {
            var forward = new GeoXLine3(P(0, 80, 0), V(1, 0, 0));
            var backward = new GeoXLine3(P(0, 80, 0), V(-1, 0, 0));

            Assert.Equal(30.0, Ball().DistanceTo(forward, Strict), 9);
            Assert.Equal(30.0, Ball().DistanceTo(backward, Strict), 9);
            Assert.False(Ball().CollidesWith(forward, Strict));
        }

        [Fact]
        public void ALineThroughTheBall_CrossesTheSurfaceTwice_InOrderAlongTheLine()
        {
            AssertPoints(new[] { P(-40, 30, 0), P(40, 30, 0) }, Ball().GetIntersections(new GeoXLine3(P(0, 30, 0), V(1, 0, 0)), Strict));
            AssertPoints(new[] { P(40, 30, 0), P(-40, 30, 0) }, Ball().GetIntersections(new GeoXLine3(P(0, 30, 0), V(-1, 0, 0)), Strict));
            AssertPoints(new[] { P(-40, 30, 0), P(40, 30, 0) }, Ball().GetIntersections(new GeoXLine3(P(1000, 30, 0), V(1, 0, 0)), Strict), 1E-9);
        }

        [Fact]
        public void ALineGrazingTheBall_GivesTheOnePointItTouchesAt()
        {
            AssertPoints(new[] { P(0, 50, 0) }, Ball().GetIntersections(new GeoXLine3(P(0, 50, 0), V(1, 0, 0)), Strict), 1E-9);
            AssertPoints(new[] { P(0, 50.0005, 0) }, Ball().GetIntersections(new GeoXLine3(P(0, 50.0005, 0), V(1, 0, 0)), Strict), 1E-9);
            Assert.Empty(Ball().GetIntersections(new GeoXLine3(P(0, 50.002, 0), V(1, 0, 0)), Strict));
        }

        [Fact]
        public void ALineThatNearlyGrazes_ButCrossesBy_AHalfMillimetre_GivesTwoPoints()
        {
            GeoPoint3[] found = Ball().GetIntersections(new GeoXLine3(P(0, 49.5, 0), V(1, 0, 0)), Strict);

            Assert.Equal(2, found.Length);
            Assert.Equal(-Math.Sqrt(49.75), found[0].X, 9);
            Assert.Equal(Math.Sqrt(49.75), found[1].X, 9);
        }

        [Fact]
        public void TheShortestLineToALine_LandsOnTheFootOfTheCentre()
        {
            AssertSegment(P(0, 50, 0), P(0, 80, 0), Ball().GetShortestLineTo(new GeoXLine3(P(0, 80, 0), V(1, 0, 0)), Strict));
            AssertSegment(P(0, 50, 0), P(0, 80, 0), Ball().GetShortestLineTo(new GeoXLine3(P(300, 80, 0), V(-1, 0, 0)), Strict));
        }

        [Fact]
        public void ALineAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var line = new GeoXLine3(P(100, 10, 5), V(-1, 0.3, 0.1));

            Assert.Equal(Ball().DistanceTo(line, Strict), line.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(line, Strict), line.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(line, Strict), line.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(line, Strict), line.GetShortestLineTo(Ball(), Strict));
            Assert.True(line.TryIntersectWith(Ball(), out GeoPoint3[] found, Strict));
            Assert.Equal(2, found.Length);
        }

        #endregion

        #region An arc, a circle, an ellipse, an edge

        private static GeoArc3 UpperHalfAboutFiftyZero() => XLine3Shapes.ArcFrom(P(50, 0, 0), GeoVector3.ZAxis, 30, P(80, 0, 0), Math.PI);

        [Fact]
        public void TheQuarterOfTheCircleOfRadiusAHundred_IsFiftyFromTheBall()
        {
            GeoArc3 quarter = XLine3Shapes.ArcFrom(P(0, 0, 0), GeoVector3.ZAxis, 100, P(100, 0, 0), Math.PI / 2.0);

            Assert.Equal(50.0, Ball().DistanceTo(quarter, Strict), 9);
            Assert.False(Ball().CollidesWith(quarter, Strict));
            Assert.Empty(Ball().GetIntersections(quarter, Strict));
        }

        [Fact]
        public void AnArcThatBeginsAndEndsOnEitherSideOfTheSurface_IsCrossedOnce()
        {
            AssertPoints(new[] { P(41, Math.Sqrt(819), 0) }, Ball().GetIntersections(UpperHalfAboutFiftyZero(), Strict), 1E-9);
            Assert.True(Ball().CollidesWith(UpperHalfAboutFiftyZero(), Strict));
        }

        [Fact]
        public void TheOtherHalfOfThatCircle_IsCrossedAtTheMirrorPoint()
        {
            GeoArc3 lower = XLine3Shapes.ArcFrom(P(50, 0, 0), GeoVector3.ZAxis, 30, P(20, 0, 0), Math.PI);

            AssertPoints(new[] { P(41, -Math.Sqrt(819), 0) }, Ball().GetIntersections(lower, Strict), 1E-9);
        }

        [Fact]
        public void ACircleInAPlaneThatOnlyTouchesTheBall_IsCrossedAtTheTouchingPoint_ButOnlyWhereItsRimPassesThroughIt()
        {
            // The ball of radius 50 about (0, 0, 50) only touches the floor, at the origin, which the circle of radius 20 about it surrounds.
            var ball = new GeoSphere3(P(0, 0, 50), 50);
            var rim = new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 20);
            var rimThrough = new GeoCircle3(P(10, 0, 0), GeoVector3.ZAxis, 10);

            Assert.Empty(ball.GetIntersections(rim, Strict));
            AssertPoints(new[] { P(0, 0, 0) }, ball.GetIntersections(rimThrough, Strict), 1E-9);
        }

        [Fact]
        public void AnArcAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            GeoArc3 arc = UpperHalfAboutFiftyZero();

            Assert.Equal(Ball().DistanceTo(arc, Strict), arc.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(arc, Strict), arc.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(arc, Strict), arc.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(arc, Strict), arc.GetShortestLineTo(Ball(), Strict));
            Assert.True(arc.TryIntersectWith(Ball(), out GeoPoint3[] found, Strict));
            Assert.Single(found);
        }

        [Fact]
        public void ACircleIsMeasuredAsADisc_SoOneStraightAboveTheBallIsFiftyFromIt()
        {
            var above = new GeoCircle3(P(0, 0, 100), GeoVector3.ZAxis, 30);
            var through = new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 30);

            Assert.Equal(50.0, Ball().DistanceTo(above, Strict), 9);
            Assert.Equal(0.0, Ball().DistanceTo(through, Strict));
            AssertSegment(P(0, 0, 50), P(0, 0, 100), Ball().GetShortestLineTo(above, Strict));
        }

        [Fact]
        public void ACircleIsCrossedWhereItsRimMeetsTheSurface()
        {
            var circle = new GeoCircle3(P(50, 0, 0), GeoVector3.ZAxis, 30);
            GeoPoint3[] found = Ball().GetIntersections(circle, Strict);

            Assert.Equal(2, found.Length);
            Assert.Contains(found, p => p.DistanceTo(P(41, Math.Sqrt(819), 0)) < 1E-9);
            Assert.Contains(found, p => p.DistanceTo(P(41, -Math.Sqrt(819), 0)) < 1E-9);
            Assert.Empty(Ball().GetIntersections(new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 30), Strict));
            Assert.True(Ball().CollidesWith(new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 30), Strict));
        }

        [Fact]
        public void ACircleAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var circle = new GeoCircle3(P(50, 0, 0), V(0.1, 0.2, 1), 30);

            Assert.Equal(Ball().DistanceTo(circle, Strict), circle.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(circle, Strict), circle.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(circle, Strict), circle.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(circle, Strict), circle.GetShortestLineTo(Ball(), Strict));
        }

        [Fact]
        public void ACurveLyingOnTheSurface_CollidesWithTheBall_AndIsCrossedNowhere_AsTwoArcsOfOneCircleAre()
        {
            var great = new GeoCircle3(P(0, 0, 0), GeoVector3.ZAxis, 50);
            var small = new GeoCircle3(P(0, 0, 30), GeoVector3.ZAxis, 40);
            GeoArc3 arc = XLine3Shapes.ArcFrom(P(0, 0, 0), GeoVector3.ZAxis, 50, P(50, 0, 0), 1.0);
            var round = new GeoEllipse3(P(0, 0, 0), GeoVector3.ZAxis, GeoVector3.XAxis, 50, 50);

            Assert.True(Ball().CollidesWith(great, Strict));
            Assert.True(Ball().CollidesWith(small, Strict));
            Assert.True(Ball().CollidesWith(arc, Strict));
            Assert.True(Ball().CollidesWith(round, Strict));
            Assert.Empty(Ball().GetIntersections(great, Strict));
            Assert.Empty(Ball().GetIntersections(small, Strict));
            Assert.Empty(Ball().GetIntersections(arc, Strict));
            Assert.Empty(Ball().GetIntersections(round, Strict));
        }

        [Fact]
        public void AnEllipseOfRadii100And40_IsCrossedAtFourPoints_InOrderFromItsMajorAxis()
        {
            var ellipse = new GeoEllipse3(P(0, 0, 0), GeoVector3.ZAxis, GeoVector3.XAxis, 100, 40);
            double x = Math.Sqrt(900.0 / 0.84);
            double y = Math.Sqrt(2500.0 - x * x);

            AssertPoints(new[] { P(x, y, 0), P(-x, y, 0), P(-x, -y, 0), P(x, -y, 0) }, Ball().GetIntersections(ellipse, Strict), 1E-7);
            Assert.Equal(0.0, Ball().DistanceTo(ellipse, Strict));
        }

        [Fact]
        public void AnEllipseAHundredAbove_IsFiftyFromTheBall_AsADisc()
        {
            var ellipse = new GeoEllipse3(P(0, 0, 100), GeoVector3.ZAxis, GeoVector3.XAxis, 100, 40);

            Assert.Equal(50.0, Ball().DistanceTo(ellipse, Strict), 9);
            AssertSegment(P(0, 0, 50), P(0, 0, 100), Ball().GetShortestLineTo(ellipse, Strict), 1E-7);
        }

        [Fact]
        public void AnEllipseAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var ellipse = new GeoEllipse3(P(10, 5, 0), V(0.2, 0.1, 1), V(1, 0, 0), 100, 40);

            Assert.Equal(Ball().DistanceTo(ellipse, Strict), ellipse.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(ellipse, Strict), ellipse.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(ellipse, Strict), ellipse.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(ellipse, Strict), ellipse.GetShortestLineTo(Ball(), Strict));
        }

        [Fact]
        public void AStraightEdgeIsAskedAsASegment_AndACurvedOneAsAnArc()
        {
            var straight = new GeoEdge3(P(0, -100, 0), P(0, 100, 0));
            var curved = new GeoEdge3(P(80, 0, 0), P(20, 0, 0), 1.0, GeoVector3.ZAxis);

            AssertPoints(new[] { P(0, -50, 0), P(0, 50, 0) }, Ball().GetIntersections(straight, Strict));
            AssertPoints(new[] { P(41, Math.Sqrt(819), 0) }, Ball().GetIntersections(curved, Strict), 1E-9);
            Assert.Equal(0.0, Ball().DistanceTo(straight, Strict));
            Assert.True(Ball().CollidesWith(curved, Strict));
        }

        [Fact]
        public void AnEdgeAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var curved = new GeoEdge3(P(80, 0, 0), P(20, 0, 0), 1.0, GeoVector3.ZAxis);

            Assert.Equal(Ball().DistanceTo(curved, Strict), curved.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(curved, Strict), curved.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(curved, Strict), curved.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(curved, Strict), curved.GetShortestLineTo(Ball(), Strict));
        }

        #endregion

        #region Chains and loops

        [Fact]
        public void APolylineIsACurve_SoTheChainAcrossAtSixtyIsTenFromTheBall()
        {
            var chain = new GeoPolyline3(P(-100, 60, 0), P(100, 60, 0));

            Assert.Equal(10.0, Ball().DistanceTo(chain, Strict), 9);
            Assert.Empty(Ball().GetIntersections(chain, Strict));
            AssertSegment(P(0, 50, 0), P(0, 60, 0), Ball().GetShortestLineTo(chain, Strict));
        }

        [Fact]
        public void APolylineIsCrossedAlongItsEdges_ACornerOnTheSurfaceOnce()
        {
            var bent = new GeoPolyline3(P(-100, 30, 0), P(100, 30, 0), P(100, 100, 0));
            var corner = new GeoPolyline3(P(-100, 0, 0), P(50, 0, 0), P(50, 100, 0));

            AssertPoints(new[] { P(-40, 30, 0), P(40, 30, 0) }, Ball().GetIntersections(bent, Strict));
            AssertPoints(new[] { P(-50, 0, 0), P(50, 0, 0) }, Ball().GetIntersections(corner, Strict), 1E-9);
        }

        [Fact]
        public void APolylineAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var bent = new GeoPolyline3(P(-100, 30, 20), P(100, 30, -20), P(100, 100, 0));

            Assert.Equal(Ball().DistanceTo(bent, Strict), bent.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(bent, Strict), bent.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(bent, Strict), bent.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(bent, Strict), bent.GetShortestLineTo(Ball(), Strict));
            Assert.True(bent.TryIntersectWith(Ball(), out GeoPoint3[] found, Strict));
            Assert.Equal(Ball().GetIntersections(bent, Strict), found);
        }

        [Fact]
        public void ACurvedChainIsCrossedAlongItsEdges_ItsArcsIncluded()
        {
            // From the origin along X to (100, 0, 0), then up as a half circle about (100, 50, 0), bulging out to (150, 50, 0).
            var hook = XLine3Shapes.Hook();

            AssertPoints(new[] { P(50, 0, 0) }, Ball().GetIntersections(hook, Strict), 1E-9);
            Assert.Equal(0.0, Ball().DistanceTo(hook, Strict));
            Assert.True(Ball().CollidesWith(hook, Strict));
        }

        [Fact]
        public void ACurvedChainThatMissesTheBall_IsMeasuredToItsPath()
        {
            var hook = XLine3Shapes.Hook().Translate(V(200, 0, 0));

            Assert.Equal(150.0, Ball().DistanceTo(hook, Strict), 9);
            Assert.Empty(Ball().GetIntersections(hook, Strict));
        }

        [Fact]
        public void ACurvedChainAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var ball = new GeoSphere3(P(60, 20, 0), 30);
            var hook = XLine3Shapes.Hook();

            Assert.Equal(ball.DistanceTo(hook, Strict), hook.DistanceTo(ball, Strict));
            Assert.Equal(ball.CollidesWith(hook, Strict), hook.CollidesWith(ball, Strict));
            Assert.Equal(ball.GetIntersections(hook, Strict), hook.GetIntersections(ball, Strict));
            AssertTurnedRound(ball.GetShortestLineTo(hook, Strict), hook.GetShortestLineTo(ball, Strict));
            Assert.True(hook.TryIntersectWith(ball, out GeoPoint3[] found, Strict));
            Assert.Equal(ball.GetIntersections(hook, Strict), found);
        }

        [Fact]
        public void ACurvedLoopIsCrossedAlongItsBoundary_InTheOrderOfItsEdges()
        {
            // The slot 200 by 50 with round ends, and the ball of radius 30 about the middle of it.
            var ball = new GeoSphere3(P(100, 25, 0), 30);
            double half = Math.Sqrt(275.0);

            AssertPoints(
                new[] { P(100 - half, 0, 0), P(100 + half, 0, 0), P(100 + half, 50, 0), P(100 - half, 50, 0) },
                ball.GetIntersections(XLine3Shapes.Slot(), Strict),
                1E-9);
            Assert.True(ball.CollidesWith(XLine3Shapes.Slot(), Strict));
        }

        [Fact]
        public void ACurvedLoopIsMeasuredToItsBoundary_AsItsOwnDistanceToAPointIs()
        {
            var ball = new GeoSphere3(P(100, 25, 0), 5);

            Assert.Equal(20.0, ball.DistanceTo(XLine3Shapes.Slot(), Strict), 9);
            Assert.Equal(ball.Center.DistanceTo(P(100, 0, 0)) - 5.0, ball.DistanceTo(XLine3Shapes.Slot(), Strict), 9);
            Assert.Empty(ball.GetIntersections(XLine3Shapes.Slot(), Strict));
        }

        [Fact]
        public void ACurvedLoopAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var ball = new GeoSphere3(P(100, 25, 0), 30);
            GeoPolygonArc3 slot = XLine3Shapes.Slot();

            Assert.Equal(ball.DistanceTo(slot, Strict), slot.DistanceTo(ball, Strict));
            Assert.Equal(ball.CollidesWith(slot, Strict), slot.CollidesWith(ball, Strict));
            Assert.Equal(ball.GetIntersections(slot, Strict), slot.GetIntersections(ball, Strict));
            AssertTurnedRound(ball.GetShortestLineTo(slot, Strict), slot.GetShortestLineTo(ball, Strict));
            Assert.True(slot.TryIntersectWith(ball, out GeoPoint3[] found, Strict));
            Assert.Equal(4, found.Length);
        }

        #endregion

        #region Flat shapes

        [Fact]
        public void ATriangleAHundredAbove_IsFiftyFromTheBall()
        {
            var triangle = new GeoTriangle3(P(0, 0, 100), P(100, 0, 100), P(0, 100, 100));

            Assert.Equal(50.0, Ball().DistanceTo(triangle), 9);
            Assert.False(Ball().CollidesWith(triangle, Strict));
            AssertSegment(P(0, 0, 50), P(0, 0, 100), Ball().GetShortestLineTo(triangle, Strict));
        }

        [Fact]
        public void ATriangleIsCrossedWhereItsSidesMeetTheSurface_InTheOrderOfItsSides()
        {
            // The side along y = -30 crosses at x = +/-40; the side up to the corner (0, 40, 0), which is inside the ball, leaves it
            // at the parameter (24200 - sqrt(85E6)) / 29800 along it, and the third side leaves it at the mirror of that.
            var triangle = new GeoTriangle3(P(-100, -30, 0), P(100, -30, 0), P(0, 40, 0));
            double t = (24200.0 - Math.Sqrt(85E6)) / 29800.0;
            GeoPoint3 right = P(100 - 100 * t, -30 + 70 * t, 0);

            AssertPoints(new[] { P(-40, -30, 0), P(40, -30, 0), right, P(-right.X, right.Y, 0) }, Ball().GetIntersections(triangle, Strict), 1E-9);
            Assert.Equal(50.0, right.DistanceTo(P(0, 0, 0)), 9);
        }

        [Fact]
        public void ATriangleHoldingTheCentre_CollidesWithTheBallAndItsBoundaryCrossesNowhereWhenItIsFarOut()
        {
            var triangle = new GeoTriangle3(P(-200, -200, 0), P(200, -200, 0), P(0, 300, 0));

            Assert.True(Ball().CollidesWith(triangle, Strict));
            Assert.Equal(0.0, Ball().DistanceTo(triangle));
            Assert.Empty(Ball().GetIntersections(triangle, Strict));
            Assert.Equal(0.0, Ball().GetShortestLineTo(triangle, Strict).Length);
        }

        [Fact]
        public void ATriangleAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var triangle = new GeoTriangle3(P(-100, -30, 20), P(100, -30, -10), P(0, 100, 0));

            Assert.Equal(Ball().DistanceTo(triangle), triangle.DistanceTo(Ball()));
            Assert.Equal(Ball().CollidesWith(triangle, Strict), triangle.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(triangle, Strict), triangle.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(triangle, Strict), triangle.GetShortestLineTo(Ball(), Strict));
            Assert.Equal(Ball().TryIntersectWith(triangle, out GeoPoint3[] ours, Strict), triangle.TryIntersectWith(Ball(), out GeoPoint3[] theirs, Strict));
            Assert.Equal(ours, theirs);
        }

        [Fact]
        public void APolygonIsReadAsASurface_SoOneHoldingTheCentreIsNoughtFromTheBall_AndOneAHundredAboveIsFifty()
        {
            Assert.Equal(0.0, Ball().DistanceTo(Floor(-100, -100, 100, 100), Strict));
            Assert.Equal(50.0, Ball().DistanceTo(Floor(-100, -100, 100, 100, 100), Strict), 9);
        }

        [Fact]
        public void APolygonIsCrossedWhereItsBoundaryMeetsTheSurface_InTheOrderOfItsSides()
        {
            GeoPoint3[] found = Ball().GetIntersections(Floor(-30, -100, 30, 100), Strict);

            AssertPoints(new[] { P(30, -40, 0), P(30, 40, 0), P(-30, 40, 0), P(-30, -40, 0) }, found, 1E-9);
        }

        [Fact]
        public void APolygonHoldingTheWholeBall_CollidesWithItAndIsCrossedNowhere()
        {
            GeoPolygon3 square = Floor(-100, -100, 100, 100);

            Assert.True(Ball().CollidesWith(square, Strict));
            Assert.Empty(Ball().GetIntersections(square, Strict));
        }

        [Fact]
        public void APolygonAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            GeoPolygon3 square = Floor(-30, -100, 30, 100);

            Assert.Equal(Ball().DistanceTo(square, Strict), square.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(square, Strict), square.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().GetIntersections(square, Strict), square.GetIntersections(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(square, Strict), square.GetShortestLineTo(Ball(), Strict));
            Assert.True(square.TryIntersectWith(Ball(), out GeoPoint3[] found, Strict));
            Assert.Equal(4, found.Length);
        }

        [Fact]
        public void AHoleIsNotMaterial_SoABallOfThirtyNearTheMiddleOfAHoleEightyWideIsFiveFromTheFace()
        {
            var face = new GeoFace3(Floor(-100, -100, 100, 100), new[] { Floor(-40, -40, 40, 40) });
            var small = new GeoSphere3(P(5, 0, 0), 30);

            Assert.Equal(5.0, small.DistanceTo(face, Strict), 9);
            Assert.False(small.CollidesWith(face, Strict));
            AssertSegment(P(35, 0, 0), P(40, 0, 0), small.GetShortestLineTo(face, Strict), 1E-9);
        }

        [Fact]
        public void AFaceIsCrossedAlongItsHoleToo()
        {
            var face = new GeoFace3(Floor(-100, -100, 100, 100), new[] { Floor(-40, -40, 40, 40) });
            GeoPoint3[] found = Ball().GetIntersections(face, Strict);

            // Each side of the hole, 40 out, meets the surface of the ball of radius 50 at 30 either side of its middle.
            Assert.Equal(8, found.Length);

            foreach (GeoPoint3 point in found)
            {
                Assert.Equal(50.0, point.DistanceTo(P(0, 0, 0)), 9);
                Assert.Equal(40.0, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)), 9);
            }
        }

        [Fact]
        public void AFaceAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var face = new GeoFace3(Floor(-100, -100, 100, 100), new[] { Floor(-40, -40, 40, 40) });
            var ball = new GeoSphere3(P(10, 5, 8), 45);

            Assert.Equal(ball.DistanceTo(face, Strict), face.DistanceTo(ball, Strict));
            Assert.Equal(ball.CollidesWith(face, Strict), face.CollidesWith(ball, Strict));
            Assert.Equal(ball.GetIntersections(face, Strict), face.GetIntersections(ball, Strict));
            AssertTurnedRound(ball.GetShortestLineTo(face, Strict), face.GetShortestLineTo(ball, Strict));
            Assert.True(face.TryIntersectWith(ball, out GeoPoint3[] found, Strict));
            Assert.Equal(ball.GetIntersections(face, Strict), found);
        }

        #endregion

        #region A plane

        [Fact]
        public void TheFloorThroughTheCentre_IsNoughtFromTheBall_AndOneEightyAboveIsThirty()
        {
            Assert.Equal(0.0, Ball().DistanceTo(GeoPlane3.XY));
            Assert.Equal(30.0, Ball().DistanceTo(new GeoPlane3(P(0, 0, 80), GeoVector3.ZAxis)), 9);
            Assert.True(Ball().CollidesWith(GeoPlane3.XY, Strict));
            Assert.False(Ball().CollidesWith(new GeoPlane3(P(0, 0, 80), GeoVector3.ZAxis), Strict));
        }

        [Fact]
        public void ABallTouchesAPlaneWithinAThousandth_AndNotWithinTwo()
        {
            Assert.True(Ball().CollidesWith(new GeoPlane3(P(0, 0, 50.0005), GeoVector3.ZAxis), Strict));
            Assert.True(Ball().CollidesWith(new GeoPlane3(P(0, 0, -50.0005), GeoVector3.ZAxis), Strict));
            Assert.False(Ball().CollidesWith(new GeoPlane3(P(0, 0, 50.002), GeoVector3.ZAxis), Strict));
        }

        [Fact]
        public void TheShortestLineToAPlane_IsTheSquareOneFromTheSurface()
        {
            var ball = new GeoSphere3(P(0, 0, 100), 50);

            AssertSegment(P(0, 0, 50), P(0, 0, 0), ball.GetShortestLineTo(GeoPlane3.XY, Strict));
            Assert.Equal(0.0, new GeoSphere3(P(3, 4, 30), 50).GetShortestLineTo(GeoPlane3.XY, Strict).Length);
            AssertNear(P(3, 4, 0), new GeoSphere3(P(3, 4, 30), 50).GetShortestLineTo(GeoPlane3.XY, Strict).StartPoint);
        }

        [Fact]
        public void ThePlaneThroughTheBall_CutsACircle_OfRadiusFortyWhereTheCentreIsThirtyAbove()
        {
            var ball = new GeoSphere3(P(0, 0, 30), 50);

            Assert.True(ball.TryGetIntersectionCircle(GeoPlane3.XY, out GeoCircle3 circle, Strict));
            AssertNear(P(0, 0, 0), circle.Center);
            Assert.Equal(40.0, circle.Radius, 9);
            Assert.True(circle.Normal.IsEqualTo(GeoVector3.ZAxis, Strict));
            Assert.False(ball.TryGetTangentPoint(GeoPlane3.XY, out _, Strict));
        }

        [Fact]
        public void ThePlaneThroughTheMiddle_CutsACircleAsBigAsTheBall_AndOneBeyondCutsNone()
        {
            Assert.True(Ball().TryGetIntersectionCircle(GeoPlane3.XY, out GeoCircle3 circle, Strict));
            Assert.Equal(50.0, circle.Radius, 12);
            Assert.False(Ball().TryGetIntersectionCircle(new GeoPlane3(P(0, 0, 80), GeoVector3.ZAxis), out _, Strict));
            Assert.False(Ball().TryGetTangentPoint(new GeoPlane3(P(0, 0, 80), GeoVector3.ZAxis), out _, Strict));
        }

        [Fact]
        public void APlaneOnlyTouchingTheBall_GivesTheOnePointItTouchesAt_AndNoCircle()
        {
            var ball = new GeoSphere3(P(0, 0, 50), 50);

            Assert.True(ball.TryGetTangentPoint(GeoPlane3.XY, out GeoPoint3 point, Strict));
            AssertNear(P(0, 0, 0), point);
            Assert.False(ball.TryGetIntersectionCircle(GeoPlane3.XY, out _, Strict));
        }

        [Fact]
        public void APlaneAHalfThousandthShortOfTheBall_AndOneAHalfThousandthInsideIt_BothTouchIt()
        {
            Assert.True(new GeoSphere3(P(0, 0, 50.0005), 50).TryGetTangentPoint(GeoPlane3.XY, out GeoPoint3 outside, Strict));
            Assert.True(new GeoSphere3(P(0, 0, 49.9995), 50).TryGetTangentPoint(GeoPlane3.XY, out GeoPoint3 inside, Strict));
            Assert.False(new GeoSphere3(P(0, 0, 50.002), 50).TryGetTangentPoint(GeoPlane3.XY, out _, Strict));
            Assert.True(new GeoSphere3(P(0, 0, 49.998), 50).TryGetIntersectionCircle(GeoPlane3.XY, out GeoCircle3 sliver, Strict));
            AssertNear(P(0, 0, 0), outside);
            AssertNear(P(0, 0, 0), inside);
            Assert.Equal(Math.Sqrt(2.0 * 50.0 * 0.002 - 0.002 * 0.002), sliver.Radius, 6);
        }

        [Fact]
        public void APlaneAsksEverythingOfTheBallTheBallAsksOfIt_ToTheBit()
        {
            var plane = new GeoPlane3(P(0, 0, 20), V(0.3, 0.2, 1));
            var ball = new GeoSphere3(P(5, 3, 40), 45);

            Assert.Equal(ball.DistanceTo(plane), plane.DistanceTo(ball));
            Assert.Equal(ball.CollidesWith(plane, Strict), plane.CollidesWith(ball, Strict));
            AssertTurnedRound(ball.GetShortestLineTo(plane, Strict), plane.GetShortestLineTo(ball, Strict));
            Assert.Equal(ball.TryGetIntersectionCircle(plane, out GeoCircle3 ours, Strict), plane.TryGetIntersectionCircle(ball, out GeoCircle3 theirs, Strict));
            Assert.Equal(ours, theirs);
            Assert.Equal(ball.TryGetTangentPoint(plane, out GeoPoint3 pointOurs, Strict), plane.TryGetTangentPoint(ball, out GeoPoint3 pointTheirs, Strict));
            Assert.Equal(pointOurs, pointTheirs);
            Assert.Equal(ball.TryGetIntersectionCircle(plane, out ours), plane.TryGetIntersectionCircle(ball, out theirs));
            Assert.Equal(ours, theirs);
        }

        #endregion

        #region Boxes and a body

        [Fact]
        public void ABoxAHundredOut_IsFiftyFromTheBall_OfEitherKind_AndAsASolid()
        {
            var aabb = new GeoAabb3(P(100, -10, -10), P(200, 10, 10));
            var obb = new GeoObb3(P(150, 0, 0), 100, 20, 20);
            GeoSolid3 solid = obb.ToSolid();

            Assert.Equal(50.0, Ball().DistanceTo(aabb), 9);
            Assert.Equal(50.0, Ball().DistanceTo(obb), 9);
            Assert.Equal(50.0, Ball().DistanceTo(solid, Strict), 9);
            Assert.False(Ball().CollidesWith(aabb, Strict));
            Assert.False(Ball().CollidesWith(obb, Strict));
            Assert.False(Ball().CollidesWith(solid, Strict));
        }

        [Fact]
        public void ABoxHoldingTheCentre_IsNoughtFromTheBall_AndAnEmptyBoxIsInfinitelyFar()
        {
            var aabb = new GeoAabb3(P(-10, -10, -10), P(10, 10, 10));
            var obb = new GeoObb3(P(0, 0, 0), 20, 20, 20);

            Assert.Equal(0.0, Ball().DistanceTo(aabb));
            Assert.Equal(0.0, Ball().DistanceTo(obb));
            Assert.Equal(0.0, Ball().DistanceTo(obb.ToSolid(), Strict));
            Assert.True(Ball().CollidesWith(aabb, Strict));
            Assert.Equal(double.PositiveInfinity, Ball().DistanceTo(GeoAabb3.Empty));
            Assert.False(Ball().CollidesWith(GeoAabb3.Empty, Strict));
            Assert.Throws<InvalidOperationException>(() => Ball().GetShortestLineTo(GeoAabb3.Empty, Strict));
        }

        [Fact]
        public void ABoxTouchesTheBallWithinAThousandth_AndNotWithinTwo()
        {
            Assert.True(Ball().CollidesWith(new GeoAabb3(P(50.0005, -10, -10), P(100, 10, 10)), Strict));
            Assert.False(Ball().CollidesWith(new GeoAabb3(P(50.002, -10, -10), P(100, 10, 10)), Strict));
        }

        [Fact]
        public void TheShortestLineToABox_LandsOnTheNearestPointOfTheBox()
        {
            AssertSegment(P(50, 0, 0), P(100, 0, 0), Ball().GetShortestLineTo(new GeoAabb3(P(100, -10, -10), P(200, 10, 10)), Strict));
            AssertSegment(P(50, 0, 0), P(100, 0, 0), Ball().GetShortestLineTo(new GeoObb3(P(150, 0, 0), 100, 20, 20), Strict));
            AssertSegment(P(50, 0, 0), P(100, 0, 0), Ball().GetShortestLineTo(new GeoObb3(P(150, 0, 0), 100, 20, 20).ToSolid(), Strict));
        }

        [Fact]
        public void TheShortestLineToABodyHoldingTheCentre_HasNoLengthAndLiesAtTheCentre()
        {
            GeoSolid3 solid = new GeoObb3(P(0, 0, 0), 20, 20, 20).ToSolid();

            Assert.Equal(P(0, 0, 0), Ball().GetShortestLineTo(solid, Strict).StartPoint);
            Assert.Equal(0.0, Ball().GetShortestLineTo(solid, Strict).Length);
        }

        [Fact]
        public void ABodyIsReadAsItsMaterial_SoABoxWithAHoleAroundTheCentreIsFarFromTheBallThatSitsInTheHole()
        {
            GeoSolid3 box = new GeoObb3(P(0, 0, 0), 200, 200, 200).ToSolid();
            GeoSolid3 hole = new GeoObb3(P(0, 0, 0), 100, 100, 100).ToSolid();
            GeoSolid3 pierced = new GeoSolid3(box.Faces, new[] { hole });
            var small = new GeoSphere3(P(0, 0, 0), 30);

            Assert.Equal(20.0, small.DistanceTo(pierced, Strict), 6);
            Assert.False(small.CollidesWith(pierced, Strict));
        }

        [Fact]
        public void ABoxAndABodyAskEverythingOfTheBallTheBallAsksOfThem_ToTheBit()
        {
            var aabb = new GeoAabb3(P(30, -10, -10), P(200, 40, 10));
            var obb = new GeoObb3(P(60, 20, 0), 100, 20, 20);
            GeoSolid3 solid = obb.ToSolid();

            Assert.Equal(Ball().DistanceTo(aabb), aabb.DistanceTo(Ball()));
            Assert.Equal(Ball().DistanceTo(obb), obb.DistanceTo(Ball()));
            Assert.Equal(Ball().DistanceTo(solid, Strict), solid.DistanceTo(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(aabb, Strict), aabb.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(obb, Strict), obb.CollidesWith(Ball(), Strict));
            Assert.Equal(Ball().CollidesWith(solid, Strict), solid.CollidesWith(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(aabb, Strict), aabb.GetShortestLineTo(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(obb, Strict), obb.GetShortestLineTo(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(solid, Strict), solid.GetShortestLineTo(Ball(), Strict));
        }

        #endregion

        #region Another ball

        [Fact]
        public void TwoBalls_AreAsFarApartAsTheirCentresLessTheirRadii_WhicheverWayRoundTheyAreAsked()
        {
            var small = new GeoSphere3(P(200, 0, 0), 10);

            Assert.Equal(140.0, Ball().DistanceTo(small), 9);
            Assert.Equal(Ball().DistanceTo(small), small.DistanceTo(Ball()));
            Assert.Equal(0.0, Ball().DistanceTo(new GeoSphere3(P(200, 0, 0), 160)));
            Assert.Equal(0.0, Ball().DistanceTo(new GeoSphere3(P(0, 0, 0), 5)));
            Assert.False(Ball().CollidesWith(small, Strict));
            Assert.True(Ball().CollidesWith(new GeoSphere3(P(60.0005, 0, 0), 10), Strict));
            Assert.False(Ball().CollidesWith(new GeoSphere3(P(60.002, 0, 0), 10), Strict));
        }

        [Fact]
        public void TheShortestLineBetweenTwoBalls_RunsAlongTheLineBetweenTheirCentres_AndTurnedRoundIsTheOneBack()
        {
            var small = new GeoSphere3(P(200, 0, 0), 10);

            AssertSegment(P(50, 0, 0), P(190, 0, 0), Ball().GetShortestLineTo(small, Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(small, Strict), small.GetShortestLineTo(Ball(), Strict));
            AssertTurnedRound(Ball().GetShortestLineTo(small), small.GetShortestLineTo(Ball()));
        }

        [Fact]
        public void TwoBallsThatOverlap_HaveAShortestLineOfNoLength_InsideBoth()
        {
            var other = new GeoSphere3(P(60, 0, 0), 30);
            GeoLine3 line = Ball().GetShortestLineTo(other, Strict);

            Assert.Equal(0.0, line.Length);
            Assert.True(Ball().Contains(line.StartPoint, Strict));
            Assert.True(other.Contains(line.StartPoint, Strict));
            AssertTurnedRound(line, other.GetShortestLineTo(Ball(), Strict));
        }

        [Fact]
        public void TwoBallsCrossingInACircle_ShareItWhicheverWayRoundTheyAreAsked_ItsNormalAlongTheLineFromTheOneAskedToTheOther()
        {
            var other = new GeoSphere3(P(50, 0, 0), 30);

            Assert.True(Ball().TryGetIntersectionCircle(other, out GeoCircle3 circle, Strict));
            AssertNear(P(41, 0, 0), circle.Center);
            Assert.Equal(Math.Sqrt(819.0), circle.Radius, 9);
            Assert.True(circle.Normal.IsEqualTo(GeoVector3.XAxis, Strict));

            Assert.True(other.TryGetIntersectionCircle(Ball(), out GeoCircle3 back, Strict));
            Assert.Equal(circle.Center, back.Center);
            Assert.Equal(circle.Radius, back.Radius);
            Assert.True(back.Normal.IsEqualTo(GeoVector3.XAxis.Negate(), Strict));
            Assert.False(Ball().TryGetTangentPoint(other, out _, Strict));
        }

        [Fact]
        public void TwoBallsOnlyTouching_GiveThePointTheyTouchAt_FromOutsideAndFromInside()
        {
            Assert.True(Ball().TryGetTangentPoint(new GeoSphere3(P(80, 0, 0), 30), out GeoPoint3 outside, Strict));
            Assert.True(Ball().TryGetTangentPoint(new GeoSphere3(P(30, 0, 0), 20), out GeoPoint3 inside, Strict));
            AssertNear(P(50, 0, 0), outside);
            AssertNear(P(50, 0, 0), inside);
            Assert.False(Ball().TryGetIntersectionCircle(new GeoSphere3(P(80, 0, 0), 30), out _, Strict));
        }

        [Theory]
        [InlineData(0.0, 0.0, 0.0, 50.0, 20.0005, 0.0, 0.0, 30.0)]
        [InlineData(0.0, 0.0, 0.0, 50.0, 1.0009, 0.0, 0.0, 49.0)]
        [InlineData(0.0, 0.0, 0.0, 100.0, 0.0105, 0.0, 0.0, 99.99)]
        [InlineData(0.0, 0.0, 0.0, 30.0, 20.0005, 0.0, 0.0, 50.0)]
        [InlineData(0.0, 0.0, 0.0, 99.99, 0.0105, 0.0, 0.0, 100.0)]
        [InlineData(0.0, 0.0, 0.0, 50.0, 60.0, 80.0, 0.0, 50.0)]
        public void TwoBallsTouchingFromInsideOrOutside_GiveAPointWithinTheToleranceOfBothSurfaces_ToTheBitFromEitherBall(
            double ax, double ay, double az, double ar, double bx, double by, double bz, double br)
        {
            var first = new GeoSphere3(P(ax, ay, az), ar);
            var second = new GeoSphere3(P(bx, by, bz), br);

            Assert.True(first.TryGetTangentPoint(second, out GeoPoint3 touch, Strict), "no touch");
            Assert.True(second.TryGetTangentPoint(first, out GeoPoint3 back, Strict));
            Assert.Equal(touch, back);
            Assert.False(first.TryGetIntersectionCircle(second, out _, Strict));
            Assert.True(Math.Abs(touch.DistanceTo(first.Center) - first.Radius) <= 1E-3, "off the first surface by " + (touch.DistanceTo(first.Center) - first.Radius));
            Assert.True(Math.Abs(touch.DistanceTo(second.Center) - second.Radius) <= 1E-3, "off the second surface by " + (touch.DistanceTo(second.Center) - second.Radius));
        }

        [Fact]
        public void TwoBallsApart_OneInsideTheOther_OrOnOneCentre_ShareNothing()
        {
            Assert.False(Ball().TryGetIntersectionCircle(new GeoSphere3(P(200, 0, 0), 30), out _, Strict));
            Assert.False(Ball().TryGetTangentPoint(new GeoSphere3(P(200, 0, 0), 30), out _, Strict));
            Assert.False(Ball().TryGetIntersectionCircle(new GeoSphere3(P(10, 0, 0), 20), out _, Strict));
            Assert.False(Ball().TryGetTangentPoint(new GeoSphere3(P(10, 0, 0), 20), out _, Strict));
            Assert.False(Ball().TryGetIntersectionCircle(new GeoSphere3(P(0, 0, 0), 20), out _, Strict));
            Assert.False(Ball().TryGetIntersectionCircle(Ball(), out _, Strict));
            Assert.False(Ball().TryGetTangentPoint(Ball(), out _, Strict));
        }

        #endregion

        #region The forms without a tolerance

        [Fact]
        public void EveryFormWithoutAToleranceAnswersWhatTheFormWithTheDefaultOneDoes()
        {
            var line = new GeoLine3(P(0, -100, 0), P(0, 100, 0));
            var xline = new GeoXLine3(P(0, 30, 0), V(1, 0, 0));
            var circle = new GeoCircle3(P(50, 0, 0), GeoVector3.ZAxis, 30);
            var chain = new GeoPolyline3(P(-100, 30, 0), P(100, 30, 0), P(100, 100, 0));
            var plane = new GeoPlane3(P(0, 0, 30), GeoVector3.ZAxis);
            var small = new GeoSphere3(P(50, 0, 0), 30);
            var arc = UpperHalfAboutFiftyZero();

            Assert.Equal(Ball().DistanceTo(line, Tolerance.Default), Ball().DistanceTo(line));
            Assert.Equal(Ball().CollidesWith(line, Tolerance.Default), Ball().CollidesWith(line));
            Assert.Equal(Ball().GetIntersections(line, Tolerance.Default), Ball().GetIntersections(line));
            Assert.Equal(Ball().GetIntersections(xline, Tolerance.Default), Ball().GetIntersections(xline));
            Assert.Equal(Ball().GetIntersections(circle, Tolerance.Default), Ball().GetIntersections(circle));
            Assert.Equal(Ball().GetIntersections(arc, Tolerance.Default), Ball().GetIntersections(arc));
            Assert.Equal(Ball().GetIntersections(chain, Tolerance.Default), Ball().GetIntersections(chain));
            Assert.Equal(Ball().GetShortestLineTo(chain, Tolerance.Default), Ball().GetShortestLineTo(chain));
            Assert.Equal(Ball().CollidesWith(small, Tolerance.Default), Ball().CollidesWith(small));
            Assert.Equal(Ball().CollidesWith(plane, Tolerance.Default), Ball().CollidesWith(plane));
            Assert.Equal(Ball().TryGetIntersectionCircle(plane, out GeoCircle3 byDefault, Tolerance.Default), Ball().TryGetIntersectionCircle(plane, out GeoCircle3 plain));
            Assert.Equal(byDefault, plain);
            Assert.Equal(Ball().TryGetIntersectionCircle(small, out byDefault, Tolerance.Default), Ball().TryGetIntersectionCircle(small, out plain));
            Assert.Equal(byDefault, plain);
            Assert.Equal(Ball().TryGetTangentPoint(small, out GeoPoint3 touchByDefault, Tolerance.Default), Ball().TryGetTangentPoint(small, out GeoPoint3 touchPlain));
            Assert.Equal(touchByDefault, touchPlain);
        }

        [Fact]
        public void ANullShape_IsRefused_ByEveryQueryThatTakesOne()
        {
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoObb3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().DistanceTo((GeoPolygonArc3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().CollidesWith((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetShortestLineTo((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetShortestLineTo((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetShortestLineTo((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetIntersections((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetIntersections((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetIntersections((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetIntersections((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => Ball().GetIntersections((GeoPolygonArc3)null));
        }

        #endregion
    }
}
