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
    /// What an arc answers: the nearest point on it, how far things are from it, where it is met, and how
    /// it is cut. The distances are checked against a brute-force sweep along both shapes, because the
    /// exact formulas pick from a handful of candidates and a missed candidate is a wrong answer rather
    /// than a slow one.
    /// </summary>
    public class Arc2OperationsTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>A quarter turn counter-clockwise about the origin, from (10, 0) to (0, 10).</summary>
        private static GeoArc2 Quarter() => new GeoArc2(GeoPoint2.Origin, 10.0, 0.0, Math.PI / 2.0);

        private static double SweptDistance(GeoArc2 arc, GeoLine2 line, int steps = 4000)
        {
            double best = double.MaxValue;

            for (int i = 0; i <= steps; i++)
            {
                GeoPoint2 onArc = arc.GetPointAtParameter((double)i / steps);
                best = Math.Min(best, Distance2.DistanceTo(line, onArc));
            }

            return best;
        }

        private static double SweptDistance(GeoArc2 first, GeoArc2 second, int steps = 2000)
        {
            double best = double.MaxValue;

            for (int i = 0; i <= steps; i++)
            {
                GeoPoint2 onFirst = first.GetPointAtParameter((double)i / steps);

                for (int j = 0; j <= steps; j += 1)
                {
                    GeoPoint2 onSecond = second.GetPointAtParameter((double)j / steps);
                    best = Math.Min(best, onFirst.GetDistanceSquaredTo(onSecond));
                }
            }

            return Math.Sqrt(best);
        }

        [Fact]
        public void TheNearestPointIsOnTheArcOrAtOneOfItsEnds()
        {
            GeoArc2 arc = Quarter();

            // Straight out from the centre through the middle of the arc.
            GeoPoint2 outward = arc.GetClosestPointOnBoundary(new GeoPoint2(100, 100));
            Assert.True(outward.IsEqualTo(arc.MidPoint, new Tolerance(1E-8, 1E-8)));
            Assert.Equal(new GeoPoint2(100, 100).DistanceTo(arc.MidPoint), arc.DistanceTo(new GeoPoint2(100, 100)), 9);

            // Round past the end: the nearer end answers, not the far side of the circle.
            Assert.True(arc.GetClosestPointOnBoundary(new GeoPoint2(-100, 1)).IsEqualTo(arc.EndPoint, Tight));
            Assert.True(arc.GetClosestPointOnBoundary(new GeoPoint2(1, -100)).IsEqualTo(arc.StartPoint, Tight));

            // Inside the circle, nearer the arc than either end.
            Assert.Equal(10.0 - Math.Sqrt(2.0), arc.DistanceTo(new GeoPoint2(1, 1)), 9);
        }

        [Fact]
        public void APointOnTheCircleButPastTheArcIsNotOnTheArc()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.IsPointOn(arc.MidPoint));
            Assert.True(arc.IsPointOn(arc.StartPoint));
            Assert.Equal(PointLocation.OnSide, arc.Locate(arc.EndPoint));

            // On the circle, a quarter turn past the end of the arc.
            Assert.False(arc.IsPointOn(new GeoPoint2(-10, 0)));
            Assert.Equal(PointLocation.OutSide, arc.Locate(new GeoPoint2(-10, 0)));

            // An arc is a curve, so nothing is ever inside it.
            Assert.Equal(PointLocation.OutSide, arc.Locate(arc.Center));
        }

        [Theory]
        [InlineData(20.0, 20.0, 40.0, -10.0)]
        [InlineData(-30.0, 5.0, -5.0, 30.0)]
        [InlineData(0.0, 15.0, 15.0, 15.0)]
        [InlineData(3.0, 3.0, 4.0, 4.0)]
        public void TheDistanceToASegmentMatchesASweepAlongTheArc(double x1, double y1, double x2, double y2)
        {
            GeoArc2 arc = Quarter();
            var line = new GeoLine2(new GeoPoint2(x1, y1), new GeoPoint2(x2, y2));

            Assert.Equal(SweptDistance(arc, line), arc.DistanceTo(line), 4);
        }

        [Fact]
        public void ASegmentCrossingTheArcIsNoDistanceFromIt()
        {
            GeoArc2 arc = Quarter();
            var crossing = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(20, 20));

            Assert.Equal(0.0, arc.DistanceTo(crossing), 12);

            // A segment crossing the circle where the arc does not reach is a real distance away.
            var pastTheEnd = new GeoLine2(new GeoPoint2(-20, -20), new GeoPoint2(-5, -5));
            Assert.True(arc.DistanceTo(pastTheEnd) > 0.0);
            Assert.Equal(SweptDistance(arc, pastTheEnd), arc.DistanceTo(pastTheEnd), 4);
        }

        [Theory]
        [InlineData(30.0, 0.0, 5.0)]
        [InlineData(0.0, 30.0, 8.0)]
        [InlineData(-25.0, -25.0, 12.0)]
        [InlineData(14.0, 14.0, 3.0)]
        public void TheDistanceBetweenTwoArcsMatchesASweepAlongBoth(double cx, double cy, double radius)
        {
            GeoArc2 first = Quarter();
            var second = new GeoArc2(new GeoPoint2(cx, cy), radius, 0.5, 4.0);

            double swept = SweptDistance(first, second, 700);

            Assert.Equal(swept, first.DistanceTo(second), 2);
        }

        [Fact]
        public void AnArcMeetsASegmentOnlyWhereItReaches()
        {
            GeoArc2 arc = Quarter();

            // A line across the middle of the quarter: one crossing on the arc.
            Assert.True(arc.TryIntersectWith(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(20, 20)), out GeoPoint2[] one));
            Assert.Single(one);
            Assert.True(one[0].IsEqualTo(arc.MidPoint, new Tolerance(1E-8, 1E-8)));

            // The same line carried on through the circle meets it twice, but only one of those is on the arc.
            Assert.True(arc.TryIntersectWith(new GeoLine2(new GeoPoint2(-20, -20), new GeoPoint2(20, 20)), out GeoPoint2[] still));
            Assert.Single(still);

            // A line meeting the circle only where the arc does not reach gives nothing.
            Assert.False(arc.TryIntersectWith(new GeoLine2(new GeoPoint2(-20, -5), new GeoPoint2(-5, -20)), out GeoPoint2[] none));
            Assert.Empty(none);

            // A line that misses the circle altogether gives nothing.
            Assert.Empty(arc.GetIntersections(new GeoLine2(new GeoPoint2(20, 0), new GeoPoint2(20, 20))));
        }

        [Fact]
        public void TwoArcsMeetOnlyWhereBothReach()
        {
            GeoArc2 arc = Quarter();

            // A circle centred on the end of the arc, crossing it.
            var through = new GeoArc2(new GeoPoint2(10, 0), 5.0, 0.0, Math.PI * 1.5);
            Assert.True(arc.TryIntersectWith(through, out GeoPoint2[] met));
            Assert.NotEmpty(met);
            Assert.All(met, p => Assert.True(arc.IsPointOn(p) && through.IsPointOn(p)));

            // Same circles, but the second arc turned away to where the first does not reach.
            var elsewhere = new GeoArc2(new GeoPoint2(10, 0), 5.0, Math.PI, Math.PI * 1.4);
            Assert.False(arc.TryIntersectWith(elsewhere, out GeoPoint2[] nothing));
            Assert.Empty(nothing);

            // Against a whole circle, the filtering is only on the arc's side.
            Assert.True(arc.TryIntersectWith(new GeoCircle2(new GeoPoint2(10, 0), 5.0), out GeoPoint2[] onCircle));
            Assert.All(onCircle, p => Assert.True(arc.IsPointOn(p)));
        }

        [Fact]
        public void CuttingAnArcGivesTwoPiecesThatAddUpToIt()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.TrySplitAt(0.25, out GeoArc2[] pieces));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(arc.Length, pieces[0].Length + pieces[1].Length, 9);
            Assert.True(pieces[0].StartPoint.IsEqualTo(arc.StartPoint, Tight));
            Assert.True(pieces[1].EndPoint.IsEqualTo(arc.EndPoint, Tight));
            Assert.True(pieces[0].EndPoint.IsEqualTo(pieces[1].StartPoint, Tight));
            Assert.Equal(arc.IsClockwise, pieces[0].IsClockwise);

            // Cutting at a point does the same, at the point of the arc nearest it.
            Assert.True(arc.TrySplitAt(new GeoPoint2(50, 50), out GeoArc2[] atPoint));
            Assert.Equal(arc.Length * 0.5, atPoint[0].Length, 6);

            // A cut at either end leaves it whole.
            Assert.False(arc.TrySplitAt(0.0, out GeoArc2[] whole));
            Assert.Single(whole);
            Assert.False(arc.TrySplitAt(1.0, out _));
            Assert.False(arc.TrySplitAt(arc.StartPoint, out _));
        }

        [Fact]
        public void ACutAndItsPiecesAgreeWithTheClockwiseDirection()
        {
            var arc = new GeoArc2(GeoPoint2.Origin, 10.0, Math.PI / 2.0, 0.0, true);

            Assert.True(arc.TrySplitAt(0.5, out GeoArc2[] pieces));
            Assert.All(pieces, p => Assert.True(p.IsClockwise));
            Assert.Equal(arc.Length, pieces[0].Length + pieces[1].Length, 9);
            Assert.True(pieces[0].EndPoint.IsEqualTo(arc.MidPoint, new Tolerance(1E-8, 1E-8)));
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgree()
        {
            GeoArc2 arc = Quarter();
            var point = new GeoPoint2(3, 9);
            var line = new GeoLine2(new GeoPoint2(0, 20), new GeoPoint2(20, 0));

            Assert.Equal(Arc2.DistanceTo(arc, point), arc.DistanceTo(point), 12);
            Assert.Equal(Arc2.DistanceTo(arc, line), arc.DistanceTo(line), 12);
            Assert.True(Arc2.ProjectToArc(arc, point).IsEqualTo(arc.GetClosestPointOnBoundary(point), Tight));
            Assert.Equal(Arc2.IsPointOn(arc, point), arc.IsPointOn(point));
            Assert.Equal(Arc2.GetIntersections(arc, line).Length, arc.GetIntersections(line).Length);
        }
    }
}
